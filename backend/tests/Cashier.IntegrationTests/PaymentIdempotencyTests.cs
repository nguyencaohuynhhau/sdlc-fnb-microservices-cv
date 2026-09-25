using System.Net;
using System.Net.Http.Json;
using Cashier.Application;
using Cashier.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cashier.IntegrationTests;

/// <summary>Tiêu chí #5: bấm "Xác nhận thu" hai lần (hay mạng gửi lại) không bao giờ ra hai bút toán.</summary>
[Trait("Category", "Integration")]
[Collection(CashierCollection.Name)]
public sealed class PaymentIdempotencyTests(CashierFixture fx) : IAsyncLifetime
{
    private readonly HttpClient _cashier = fx.ClientAs("Cashier", "cashier");
    private Guid _shiftId;

    private sealed record ReceiptDto(Guid PaymentId, Guid OrderId, Guid ShiftId, decimal Amount, string Method);

    public async Task InitializeAsync()
    {
        fx.Orders.Reset();
        _shiftId = await fx.OpenFreshShiftAsync(_cashier);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task<HttpResponseMessage> Pay(string? key, Guid orderId, decimal expectedTotal, string method = "Cash", HttpClient? client = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { orderId, expectedTotal, method }),
        };
        if (key is not null)
        {
            req.Headers.Add("Idempotency-Key", key);
        }

        return (client ?? _cashier).SendAsync(req);
    }

    private static async Task<string?> Detail(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;

    private Task<int> PaymentsFor(Guid orderId) => fx.WithDbAsync(db => db.Payments.CountAsync(p => p.OrderId == orderId));

    private Task<int> KeysFor(Guid key) => fx.WithDbAsync(db => db.IdempotencyKeys.CountAsync(k => k.Key == key));

    [Fact]
    public async Task Payments_ConcurrentSameKey_CreatesOneLedgerEntry()
    {
        var orderId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        fx.Orders.Delay = TimeSpan.FromMilliseconds(300); // giữ request đầu trong transaction để 4 cái sau chắc chắn chen vào

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Pay(key, orderId, 45_000m)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        bodies.Distinct().Should().ContainSingle("gửi lại phải nhận nguyên văn phản hồi lần đầu");
        (await PaymentsFor(orderId)).Should().Be(1);
        fx.Orders.Calls.Should().Be(1, "4 request sau chờ ở khoá key rồi trả lại phản hồi đã lưu, không gọi ordering");

        var receipt = (await responses[0].Content.ReadFromJsonAsync<ReceiptDto>())!;
        receipt.Should().BeEquivalentTo(new ReceiptDto(Guid.Parse(key), orderId, _shiftId, 45_000m, "Cash"));
    }

    [Fact]
    public async Task Payments_SameKeyDifferentBody_Returns422()
    {
        var orderId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        (await Pay(key, orderId, 45_000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Cùng số tiền khác cách viết (45000 vs 45000.00) vẫn là cùng một yêu cầu.
        (await Pay(key, orderId, 45_000.00m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var other = await Pay(key, orderId, 45_000m, method: "Transfer");
        other.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Detail(other)).Should().Be(PayOrderHandler.KeyMismatchMessage);
        (await PaymentsFor(orderId)).Should().Be(1);
    }

    [Fact]
    public async Task Payments_MissingKey_Returns400()
    {
        var orderId = Guid.NewGuid();

        foreach (var key in new[] { null, "not-a-uuid" })
        {
            var res = await Pay(key, orderId, 45_000m);
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Detail(res)).Should().Be("Yêu cầu không hợp lệ.");
        }

        fx.Orders.Calls.Should().Be(0);
        (await PaymentsFor(orderId)).Should().Be(0);
    }

    [Fact]
    public async Task Payments_KitchenOrAnonymous_Rejected()
    {
        (await Pay(Guid.NewGuid().ToString(), Guid.NewGuid(), 45_000m, client: fx.ClientAs("Kitchen"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Pay(Guid.NewGuid().ToString(), Guid.NewGuid(), 45_000m, client: fx.Factory.CreateClient())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Payments_TotalMismatch_Returns409()
    {
        var orderId = Guid.NewGuid();
        var key = Guid.NewGuid();
        fx.Orders.Totals[orderId] = 90_000m; // tab khác vừa thêm món

        var res = await Pay(key.ToString(), orderId, 45_000m);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(res)).Should().Be("Đơn vừa thay đổi, tổng tiền hiện tại là 90.000đ. Kiểm tra lại rồi thu tiền.");
        (await PaymentsFor(orderId)).Should().Be(0);
        (await KeysFor(key)).Should().Be(0, "lỗi không được lưu làm phản hồi — thu ngân sửa số rồi bấm lại cùng form");
    }

    [Fact]
    public async Task Payments_NoOpenShift_Returns409()
    {
        await fx.WithDbAsync(db => db.Shifts.Where(s => s.ClosedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.ClosedAt, DateTimeOffset.UtcNow)));
        var orderId = Guid.NewGuid();

        var res = await Pay(Guid.NewGuid().ToString(), orderId, 45_000m);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(res)).Should().Be(PayOrderHandler.NoOpenShiftMessage);
        fx.Orders.Calls.Should().Be(0, "chưa có ca thì không được đánh dấu đơn đã trả bên ordering");
    }

    [Fact]
    public async Task Payments_LostReply_RetrySameKeyCompletes()
    {
        var orderId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        fx.Orders.LoseNextReply = true; // ordering đã Paid, nhưng cashier hết giờ chờ

        var first = await Pay(key, orderId, 45_000m);
        first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await PaymentsFor(orderId)).Should().Be(0);

        // Thu ngân bấm lại cùng form (cùng key) → ordering nhận ra cùng paymentId → Ok → ghi bút toán.
        var retry = await Pay(key, orderId, 45_000m);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PaymentsFor(orderId)).Should().Be(1);
    }

    [Fact]
    public async Task Payments_OrderingUnavailable_Returns503_NoLedgerEntry()
    {
        var orderId = Guid.NewGuid();
        var key = Guid.NewGuid();
        fx.Orders.LoseNextReply = false;

        var res = await Pay(key.ToString(), orderId, 45_000m);

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await Detail(res)).Should().Be(PayOrderHandler.OrderingUnavailableMessage);
        (await PaymentsFor(orderId)).Should().Be(0);
        (await KeysFor(key)).Should().Be(0);
    }

    [Fact]
    public async Task Payments_TwoKeysSameOrder_OnlyOneCompleted()
    {
        var orderId = Guid.NewGuid();
        fx.Orders.Delay = TimeSpan.FromMilliseconds(100);

        // Hai máy POS cùng thu một đơn.
        var responses = await Task.WhenAll(Pay(Guid.NewGuid().ToString(), orderId, 45_000m), Pay(Guid.NewGuid().ToString(), orderId, 45_000m));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        (await Detail(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict))).Should().Be(PayOrderHandler.AlreadyPaidMessage);
        (await PaymentsFor(orderId)).Should().Be(1);

        // Chốt chặn thứ hai: kể cả ordering trả nhầm Ok, DB không nhận bút toán Completed thứ hai cho cùng đơn.
        var act = () => fx.WithDbAsync(db =>
        {
            db.Payments.Add(Payment.Completed(Guid.NewGuid(), orderId, _shiftId, 45_000m, PaymentMethod.Cash, DateTimeOffset.UtcNow));
            return db.SaveChangesAsync();
        });
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
