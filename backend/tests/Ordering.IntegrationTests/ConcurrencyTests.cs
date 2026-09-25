using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application;
using Ordering.Domain;

namespace Ordering.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(OrderingCollection.Name)]
public sealed class ConcurrencyTests(OrderingFixture fx) : IAsyncLifetime
{
    private readonly HttpClient _cashier = fx.ClientAs("Cashier");
    private readonly HttpClient _kitchen = fx.ClientAs("Kitchen");

    public Task InitializeAsync() => fx.OpenShiftAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static HttpRequestMessage Write(HttpMethod method, string url, uint version, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return req;
    }

    private static async Task<string?> Detail(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;

    [Fact]
    public async Task Order_ConcurrentUpdate_SecondWriterGets409()
    {
        var order = await fx.CreateOrderAsync(_cashier, [.. Enumerable.Repeat((fx.Latte, 1), 5)]);

        // Năm người cùng cầm một phiên bản, mỗi người huỷ một món khác nhau: chỉ người đầu tiên được ghi.
        var responses = await Task.WhenAll(order.Items.Select(i =>
            _cashier.SendAsync(Write(HttpMethod.Delete, $"/api/orders/{order.Id}/items/{i.Id}", order.Version))));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        var rejected = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        rejected.Should().HaveCount(4);
        (await Detail(rejected[0])).Should().Be(Order.StaleVersionMessage);

        var after = await _cashier.GetAsync($"/api/orders/{order.Id}");
        var current = (await after.Content.ReadFromJsonAsync<OrderView>())!;
        current.Items.Count(i => i.Status == "Cancelled").Should().Be(1);
        current.Total.Should().Be(4 * 45_000m);
        current.Version.Should().NotBe(order.Version);
        after.Headers.ETag!.Tag.Should().Be($"\"{current.Version}\"");
    }

    [Fact]
    public async Task Order_TwoWritersLoadedSameVersion_SecondSaveRejectedByXmin()
    {
        var created = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1), (fx.Croissant, 1));

        // Hai request đã qua bước so If-Match cùng lúc — chỉ còn xmin trong câu UPDATE chặn được.
        using var a = fx.Factory.Services.CreateScope();
        using var b = fx.Factory.Services.CreateScope();
        var repoA = a.ServiceProvider.GetRequiredService<IOrderRepository>();
        var repoB = b.ServiceProvider.GetRequiredService<IOrderRepository>();
        var orderA = (await repoA.FindAsync(created.Id, default))!;
        var orderB = (await repoB.FindAsync(created.Id, default))!;

        orderA.CancelItem(orderA.Items[0].Id, DateTimeOffset.UtcNow);
        orderB.SetItemStatus(orderB.Items[1].Id, OrderItemStatus.Preparing, DateTimeOffset.UtcNow);

        (await repoA.TrySaveAsync(default)).Should().BeTrue();
        (await repoB.TrySaveAsync(default)).Should().BeFalse();
    }

    [Fact]
    public async Task KitchenWithStaleVersion_Gets409_ThenSucceedsWithFreshVersion()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1), (fx.Croissant, 1));
        var added = await _cashier.SendAsync(Write(HttpMethod.Post, $"/api/orders/{order.Id}/items", order.Version, new { menuItemId = fx.Croissant.Id, qty = 2 }));
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var fresh = (await added.Content.ReadFromJsonAsync<OrderView>())!;

        var stale = await _kitchen.SendAsync(Write(HttpMethod.Patch, $"/api/orders/{order.Id}/items/{order.Items[0].Id}/status", order.Version, new { status = "Preparing" }));
        var ok = await _kitchen.SendAsync(Write(HttpMethod.Patch, $"/api/orders/{order.Id}/items/{order.Items[0].Id}/status", fresh.Version, new { status = "Preparing" }));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ok.Content.ReadFromJsonAsync<OrderView>())!.Items[0].Status.Should().Be("Preparing");
        fresh.Total.Should().Be(45_000m + (3 * 30_000m));
    }

    [Fact]
    public async Task WriteWithoutIfMatch_Returns428()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));

        var res = await _cashier.PostAsync($"/api/orders/{order.Id}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        (await Detail(res)).Should().Be("Thiếu phiên bản đơn. Tải lại rồi thử lại nhé.");
    }

    [Fact]
    public async Task CancelledItemAlreadyCooked_Returns409_AndKitchenCannotCreateOrders()
    {
        var order = await fx.CreateOrderAsync(_cashier, (fx.Latte, 1));
        var cooking = await _kitchen.SendAsync(Write(HttpMethod.Patch, $"/api/orders/{order.Id}/items/{order.Items[0].Id}/status", order.Version, new { status = "Preparing" }));
        var v = (await cooking.Content.ReadFromJsonAsync<OrderView>())!.Version;

        var cancel = await _cashier.SendAsync(Write(HttpMethod.Delete, $"/api/orders/{order.Id}/items/{order.Items[0].Id}", v));

        cancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(cancel)).Should().Be(Order.ItemAlreadyCookedMessage);
        (await _kitchen.PostAsJsonAsync("/api/orders", new { items = new[] { new { menuItemId = fx.Latte.Id, qty = 1 } } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
