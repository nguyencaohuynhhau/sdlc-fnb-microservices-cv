using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Fnb.Ordering.V1;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Ordering.Application;
using Ordering.Domain;
using Ordering.Infrastructure;
using Shared.Kernel;
using Shared.Messaging;

namespace Ordering.IntegrationTests;

/// <summary>gRPC <c>MarkPaid</c> gọi thật qua TestServer — đúng đường cashier sẽ đi.</summary>
[Trait("Category", "Integration")]
[Collection(OrderingCollection.Name)]
public sealed class MarkPaidTests(OrderingFixture fx) : IAsyncLifetime
{
    private readonly HttpClient _cashier = fx.ClientAs("Cashier");
    private readonly HttpClient _kitchen = fx.ClientAs("Kitchen");

    public Task InitializeAsync() => fx.OpenShiftAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private OrderPayments.OrderPaymentsClient Grpc() =>
        new(GrpcChannel.ForAddress(fx.Factory.Server.BaseAddress, new GrpcChannelOptions { HttpHandler = fx.Factory.Server.CreateHandler() }));

    private async Task<MarkPaidReply> MarkPaid(Guid orderId, string expectedTotal, Guid paymentId, string? role = "Cashier") =>
        await Grpc().MarkPaidAsync(
            new MarkPaidRequest { OrderId = orderId.ToString(), ExpectedTotal = expectedTotal, PaymentId = paymentId.ToString() },
            role is null ? new Metadata() : new Metadata { { "Authorization", $"Bearer {OrderingFixture.TokenFor(role)}" } });

    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private async Task<OrderView> GetAsync(Guid id) =>
        (await _cashier.GetFromJsonAsync<OrderView>($"/api/orders/{id}"))!;

    [Fact]
    public async Task MarkPaid_TotalMismatch_ReturnsActualTotal()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 2));

        var reply = await MarkPaid(order.Id, "45000", Guid.NewGuid());

        reply.ResultCase.Should().Be(MarkPaidReply.ResultOneofCase.TotalMismatch);
        reply.TotalMismatch.ActualTotal.Should().Be("90000.00");
        (await GetAsync(order.Id)).Status.Should().Be("Open");
    }

    [Fact]
    public async Task MarkPaid_SamePaymentIdTwice_IsIdempotent()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));
        var paymentId = Guid.NewGuid();

        (await MarkPaid(order.Id, Money(order.Total), paymentId)).ResultCase.Should().Be(MarkPaidReply.ResultOneofCase.Ok);
        var paid = await GetAsync(order.Id);

        // Cashier mất phản hồi rồi gửi lại cùng key: vẫn Ok, không đổi gì thêm.
        (await MarkPaid(order.Id, Money(order.Total), paymentId)).ResultCase.Should().Be(MarkPaidReply.ResultOneofCase.Ok);
        var again = await GetAsync(order.Id);
        again.Status.Should().Be("Paid");
        again.Version.Should().Be(paid.Version);
        again.PaidAt.Should().Be(paid.PaidAt);
    }

    [Fact]
    public async Task MarkPaid_OtherPaymentId_AlreadyPaid()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Croissant, 1));
        await MarkPaid(order.Id, Money(order.Total), Guid.NewGuid());

        var reply = await MarkPaid(order.Id, Money(order.Total), Guid.NewGuid());

        reply.ResultCase.Should().Be(MarkPaidReply.ResultOneofCase.AlreadyPaid);
    }

    [Fact]
    public async Task MarkPaid_WritesOrderPaidToOutbox()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1), (fx.Croissant, 2));
        var paymentId = Guid.NewGuid();

        await MarkPaid(order.Id, Money(order.Total), paymentId);

        OutboxMessage? row = null;
        await fx.WithDbAsync(async db => row = await db.Set<OutboxMessage>().AsNoTracking()
            .SingleOrDefaultAsync(m => m.Topic == Topics.OrderPaid && m.Key == order.Id.ToString()));
        row.Should().NotBeNull("OrderPaid phải vào outbox cùng transaction với Status = Paid");
        row!.Payload.Should().Contain(paymentId.ToString()).And.Contain(fx.Croissant.Id.ToString());
    }

    [Fact]
    public async Task MarkPaid_WhileKitchenUpdates_RetriesAndSucceeds()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));
        var kitchen = new KitchenWritesFirst(order.Id, times: 2); // bếp chen hai lần liên tiếp

        var (outcome, _) = await MarkPaidWith(kitchen, order);

        outcome.Should().Be(MarkPaidOutcome.Ok);
        kitchen.Writes.Should().Be(2, "xung đột phải thật sự xảy ra");
        (await GetAsync(order.Id)).Status.Should().Be("Paid");
    }

    [Fact]
    public async Task MarkPaid_ConflictEveryAttempt_GivesUpWithStaleVersion()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));
        var kitchen = new KitchenWritesFirst(order.Id, times: int.MaxValue);

        var act = () => MarkPaidWith(kitchen, order);

        (await act.Should().ThrowAsync<DomainException>()).WithMessage(Order.StaleVersionMessage);
        kitchen.Writes.Should().Be(OrderService.MarkPaidAttempts);
        (await GetAsync(order.Id)).Status.Should().Be("Open");
    }

    [Fact]
    public async Task MarkPaid_ConcurrentDifferentPayments_OnlyOneWins()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));

        // Hai máy POS cùng bấm thu một đơn với hai key khác nhau.
        var replies = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => MarkPaid(order.Id, Money(order.Total), Guid.NewGuid())));

        replies.Count(r => r.ResultCase == MarkPaidReply.ResultOneofCase.Ok).Should().Be(1);
        replies.Count(r => r.ResultCase == MarkPaidReply.ResultOneofCase.AlreadyPaid).Should().Be(4);
    }

    [Fact]
    public async Task MarkPaid_WithoutToken_Unauthenticated()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));

        var anonymous = () => MarkPaid(order.Id, Money(order.Total), Guid.NewGuid(), role: null);
        var kitchen = () => MarkPaid(order.Id, Money(order.Total), Guid.NewGuid(), role: "Kitchen");

        (await anonymous.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
        (await kitchen.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.PermissionDenied);
        (await GetAsync(order.Id)).Status.Should().Be("Open");
    }

    [Fact]
    public async Task MarkPaid_TotalWithDifferentScale_Matches()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 2));

        // Tổng 90000.00 trong DB; cashier gửi "90000" — cùng số tiền, không được coi là lệch.
        var reply = await MarkPaid(order.Id, "90000", Guid.NewGuid());

        reply.ResultCase.Should().Be(MarkPaidReply.ResultOneofCase.Ok);
    }

    [Fact]
    public async Task Kitchen_PaidOrderWithPendingItems_StillVisible()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));
        await MarkPaid(order.Id, Money(order.Total), Guid.NewGuid());

        (await _kitchen.GetFromJsonAsync<List<OrderView>>("/api/kitchen/orders"))!.Should().Contain(o => o.Id == order.Id);
        (await _cashier.GetFromJsonAsync<List<OrderView>>("/api/orders?active=true"))!.Should().Contain(o => o.Id == order.Id);

        // Khách trả trước, bếp vẫn làm tiếp; món xong hết thì đơn rời màn hình bếp.
        foreach (var status in new[] { "Preparing", "Done" })
        {
            var current = await GetAsync(order.Id);
            var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{order.Id}/items/{current.Items[0].Id}/status")
            {
                Content = JsonContent.Create(new { status }),
            };
            req.Headers.TryAddWithoutValidation("If-Match", $"\"{current.Version}\"");
            (await _kitchen.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _kitchen.GetFromJsonAsync<List<OrderView>>("/api/kitchen/orders"))!.Should()
                .Match<List<OrderView>>(l => l.Any(o => o.Id == order.Id) == (status == "Preparing"));
        }

        (await _kitchen.GetFromJsonAsync<List<OrderView>>("/api/kitchen/orders"))!.Should().NotContain(o => o.Id == order.Id);
        (await _cashier.GetFromJsonAsync<List<OrderView>>("/api/orders?active=true"))!.Should().NotContain(o => o.Id == order.Id);
    }

    /// <summary>
    /// Chạy <see cref="OrderService.MarkPaidAsync"/> trên một DbContext có thêm <paramref name="kitchen"/> — cùng
    /// interceptor outbox và các dịch vụ thật của host, chỉ khác là bếp "chen ngang" trước mỗi lần lưu.
    /// </summary>
    private async Task<(MarkPaidOutcome, decimal)> MarkPaidWith(KitchenWritesFirst kitchen, OrderView order)
    {
        using var scope = fx.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql(sp.GetRequiredService<OrderingDbContext>().Database.GetConnectionString())
            .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>(), kitchen)
            .Options;
        await using var db = new OrderingDbContext(options);
        var service = new OrderService(
            new OrderRepository(db),
            sp.GetRequiredService<ICurrentShift>(),
            sp.GetRequiredService<IMenuCatalog>(),
            sp.GetRequiredService<IOrderNotifier>(),
            TimeProvider.System);
        return await service.MarkPaidAsync(order.Id, order.Total, Guid.NewGuid(), default);
    }

    /// <summary>Ngay trước khi EF lưu đơn, một connection khác (như request của bếp) ghi lên cùng dòng → xmin đổi thật.</summary>
    private sealed class KitchenWritesFirst(Guid orderId, int times) : SaveChangesInterceptor
    {
        public int Writes { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            if (Writes < times && db.ChangeTracker.Entries<Order>().Any(e => e.Entity.Id == orderId && e.State == EntityState.Modified))
            {
                Writes++;
                await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
                await conn.OpenAsync(cancellationToken);
                await using var cmd = new NpgsqlCommand("UPDATE orders SET status = status WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("id", orderId);
                (await cmd.ExecuteNonQueryAsync(cancellationToken)).Should().Be(1);
            }

            return result;
        }
    }
}
