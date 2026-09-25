using System.Net;
using System.Net.WebSockets;
using FluentAssertions;

namespace Gateway.Tests;

public sealed class GatewayTests(GatewayFixture fx) : IClassFixture<GatewayFixture>
{
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/refresh")]
    public async Task LoginAndRefresh_AreAnonymous_ReachIdentity(string path)
    {
        var res = await fx.Client().PostAsync(path, null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Be($"identity {path}");
    }

    [Theory]
    [InlineData("/api/auth/logout")]
    [InlineData("/api/menu")]
    [InlineData("/api/orders")]
    [InlineData("/api/kitchen/orders")]
    [InlineData("/api/shifts/current")]
    [InlineData("/api/payments")]
    [InlineData("/hubs/orders/negotiate")]
    public async Task EverythingElse_WithoutToken_Returns401(string path)
    {
        var res = await fx.Client().GetAsync(path);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/menu", "ordering")]
    [InlineData("/api/orders", "ordering")]
    [InlineData("/api/orders/5f0c/items", "ordering")]
    [InlineData("/api/kitchen/orders", "ordering")]
    [InlineData("/hubs/orders/negotiate", "ordering")]
    [InlineData("/api/shifts/current", "cashier")]
    [InlineData("/api/payments", "cashier")]
    [InlineData("/api/auth/logout", "identity")]
    public async Task WithToken_RoutesToOwningService(string path, string service)
    {
        var res = await fx.Client(GatewayFixture.Token()).GetAsync(path);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Be($"{service} {path}");
    }

    [Fact]
    public async Task TokenSignedWithOtherKey_Returns401()
    {
        var forged = GatewayFixture.Token("Owner", key: "attacker-key-attacker-key-attacker-key-0123");

        var res = await fx.Client(forged).GetAsync("/api/menu");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryStringToken_AcceptedOnlyOnHubs()
    {
        var token = GatewayFixture.Token();

        (await fx.Client().GetAsync($"/api/menu?access_token={token}")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "token trong URL chỉ dành cho WebSocket của /hubs");
        (await fx.Client().GetAsync($"/hubs/orders/negotiate?access_token={token}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HubWebSocket_UpgradesThroughGateway_AndTokenNeverLogged()
    {
        var token = GatewayFixture.Token("Kitchen");
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri($"ws://{fx.BaseAddress.Authority}/hubs/orders?access_token={token}"), default);
        var frame = await ws.ReceiveAsync(new byte[16], default);

        frame.MessageType.Should().Be(WebSocketMessageType.Close);
        ws.CloseStatusDescription.Should().Be("ordering", "kết nối WebSocket phải đi tới dịch vụ ordering");
        fx.Logs.Lines.Should().NotContain(l => l.Contains(token), "token trong query không được ghi vào log gateway");
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginOnly()
    {
        async Task<string?> AllowedOrigin(string origin)
        {
            using var req = new HttpRequestMessage(HttpMethod.Options, "/api/orders");
            req.Headers.Add("Origin", origin);
            req.Headers.Add("Access-Control-Request-Method", "POST");
            var res = await fx.Client().SendAsync(req);
            return res.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? v.Single() : null;
        }

        (await AllowedOrigin("http://localhost:5173")).Should().Be("http://localhost:5173");
        (await AllowedOrigin("https://evil.example")).Should().BeNull();
    }

    [Fact]
    public async Task Healthz_AllServicesUp_Returns200()
    {
        var res = await fx.Client().GetAsync("/healthz");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Healthz_OneServiceDown_Returns503()
    {
        Environment.SetEnvironmentVariable("Services__Cashier", "http://127.0.0.1:1");
        var (factory, address) = fx.StartGateway();
        Environment.SetEnvironmentVariable("Services__Cashier", fx.ServiceUrls["Cashier"]);
        await using var _ = factory;

        var res = await new HttpClient { BaseAddress = address }.GetAsync("/healthz");

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await res.Content.ReadAsStringAsync()).Should().Contain("\"cashier\":\"down\"");
    }
}
