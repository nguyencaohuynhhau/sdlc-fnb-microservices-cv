using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cashier.Domain;
using Cashier.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel;
using Shared.Messaging;

namespace Cashier.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(CashierCollection.Name)]
public sealed class ShiftTests(CashierFixture fx) : IAsyncLifetime
{
    private readonly HttpClient _cashier = fx.ClientAs("Cashier", "cashier");

    private sealed record ShiftDto(Guid Id, string OpenedBy, DateTimeOffset? ClosedAt, decimal OpeningFloat, decimal? Variance);

    private Task<HttpResponseMessage> Open(HttpClient client, decimal openingFloat = 500_000m) =>
        client.PostAsJsonAsync("/api/shifts/open", new { openingFloat });

    private sealed record SummaryDto(
        Guid Id, decimal? CountedCash, decimal? ExpectedCash, decimal? Variance, int OrderCount, decimal Revenue, decimal CashTotal, decimal TransferTotal);

    private Task<HttpResponseMessage> Close(Guid id, decimal countedCash = 500_000m) =>
        _cashier.PostAsJsonAsync($"/api/shifts/{id}/close", new { countedCash });

    private Task<HttpResponseMessage> Pay(Guid orderId, decimal expectedTotal, string method)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments") { Content = JsonContent.Create(new { orderId, expectedTotal, method }) };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return _cashier.SendAsync(req);
    }

    private static async Task<string?> Detail(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;

    /// <summary>Mỗi ca kiểm thử bắt đầu khi không còn ca nào mở.</summary>
    public async Task InitializeAsync()
    {
        fx.Orders.Reset();
        using var scope = fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CashierDbContext>();
        await db.Shifts.Where(s => s.ClosedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.ClosedAt, DateTimeOffset.UtcNow));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpenShift_Concurrent_OnlyOneSucceeds()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Open(_cashier)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var rejected = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        rejected.Should().HaveCount(9);
        (await Detail(rejected[0])).Should().Be("Đang có ca mở. Đóng ca hiện tại trước.");

        using var scope = fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CashierDbContext>();
        (await db.Shifts.CountAsync(s => s.ClosedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task OpenShift_WritesShiftOpenedToOutbox_AndCurrentReturnsIt()
    {
        var res = await Open(_cashier, 300_000m);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var shift = (await res.Content.ReadFromJsonAsync<ShiftDto>())!;
        shift.OpenedBy.Should().Be("cashier");
        shift.OpeningFloat.Should().Be(300_000m);
        shift.Variance.Should().BeNull();

        var current = await fx.ClientAs("Kitchen").GetFromJsonAsync<ShiftDto>("/api/shifts/current");
        current!.Id.Should().Be(shift.Id);

        using var scope = fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CashierDbContext>();
        var outbox = await db.Set<OutboxMessage>().SingleAsync(m => m.Key == shift.Id.ToString());
        outbox.Topic.Should().Be(Topics.ShiftOpened);
        outbox.Type.Should().Be(nameof(ShiftOpened));
    }

    [Fact]
    public async Task CloseShift_Twice_SecondGets409_AndCurrentBecomes204()
    {
        var shift = (await (await Open(_cashier)).Content.ReadFromJsonAsync<ShiftDto>())!;

        var first = await Close(shift.Id);
        var second = await Close(shift.Id);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ShiftDto>())!.ClosedAt.Should().NotBeNull();
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(second)).Should().Be("Ca này đã đóng.");
        (await _cashier.GetAsync("/api/shifts/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CloseShift_ReconcilesCash()
    {
        var shiftId = await fx.OpenFreshShiftAsync(_cashier, openingFloat: 0m);
        (await Pay(Guid.NewGuid(), 45_000m, "Cash")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Pay(Guid.NewGuid(), 30_000m, "Cash")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Pay(Guid.NewGuid(), 50_000m, "Transfer")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Két phải có 0 + 75.000 tiền mặt; thu ngân đếm được 70.000 → thiếu 5.000. Chuyển khoản không vào két.
        var res = await Close(shiftId, countedCash: 70_000m);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<SummaryDto>()).Should().BeEquivalentTo(
            new SummaryDto(shiftId, 70_000m, 75_000m, -5_000m, OrderCount: 3, Revenue: 125_000m, CashTotal: 75_000m, TransferTotal: 50_000m));

        var outbox = await fx.WithDbAsync(db => db.Set<OutboxMessage>()
            .SingleAsync(m => m.Key == shiftId.ToString() && m.Topic == Topics.ShiftClosed));
        JsonDocument.Parse(outbox.Payload).RootElement.GetProperty("variance").GetDecimal().Should().Be(-5_000m);
    }

    [Fact]
    public async Task CloseShift_ConcurrentWithPayments_NoPaymentAfterClose()
    {
        var shiftId = await fx.OpenFreshShiftAsync(_cashier, openingFloat: 0m);
        fx.Orders.Delay = TimeSpan.FromMilliseconds(500); // lần thu đang chờ ordering, đang giữ ca

        var paying = Pay(Guid.NewGuid(), 45_000m, "Cash");
        await Task.Delay(100);
        var closing = await Close(shiftId, countedCash: 45_000m);

        (await paying).StatusCode.Should().Be(HttpStatusCode.OK);
        closing.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = (await closing.Content.ReadFromJsonAsync<SummaryDto>())!;
        summary.CashTotal.Should().Be(45_000m, "đóng ca phải chờ lần thu đang dở commit rồi mới cộng");
        summary.Variance.Should().Be(0m);
    }

    [Fact]
    public async Task CloseShift_MissingCountedCash_Returns400()
    {
        var shiftId = await fx.OpenFreshShiftAsync(_cashier);

        var res = await _cashier.PostAsJsonAsync($"/api/shifts/{shiftId}/close", new { });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _cashier.GetAsync("/api/shifts/current")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloseShift_Unknown_Returns404()
    {
        var res = await Close(Guid.NewGuid());

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Detail(res)).Should().Be("Không tìm thấy ca.");
    }

    [Fact]
    public async Task Kitchen_CannotOpenShift_AndAnonymousIsRejected()
    {
        (await Open(fx.ClientAs("Kitchen"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Open(fx.Factory.CreateClient())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
