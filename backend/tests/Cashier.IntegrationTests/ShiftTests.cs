using System.Net;
using System.Net.Http.Json;
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
public sealed class ShiftTests(CashierFixture fx) : IClassFixture<CashierFixture>, IAsyncLifetime
{
    private readonly HttpClient _cashier = fx.ClientAs("Cashier", "cashier");

    private sealed record ShiftDto(Guid Id, string OpenedBy, DateTimeOffset? ClosedAt, decimal OpeningFloat, decimal? Variance);

    private Task<HttpResponseMessage> Open(HttpClient client, decimal openingFloat = 500_000m) =>
        client.PostAsJsonAsync("/api/shifts/open", new { openingFloat });

    private static async Task<string?> Detail(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;

    /// <summary>Mỗi ca kiểm thử bắt đầu khi không còn ca nào mở.</summary>
    public async Task InitializeAsync()
    {
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

        var first = await _cashier.PostAsync($"/api/shifts/{shift.Id}/close", null);
        var second = await _cashier.PostAsync($"/api/shifts/{shift.Id}/close", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ShiftDto>())!.ClosedAt.Should().NotBeNull();
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(second)).Should().Be("Ca này đã đóng.");
        (await _cashier.GetAsync("/api/shifts/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CloseShift_Unknown_Returns404()
    {
        var res = await _cashier.PostAsync($"/api/shifts/{Guid.NewGuid()}/close", null);

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
