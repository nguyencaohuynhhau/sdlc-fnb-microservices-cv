using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Web;

namespace Gateway.Tests;

/// <summary>
/// Gateway thật trên Kestrel (WebSocket cần socket thật, TestServer không nâng cấp được qua YARP),
/// trỏ vào ba dịch vụ giả. Mỗi dịch vụ giả trả lại "tên-dịch-vụ đường-dẫn" để test biết request đi đâu.
/// </summary>
public sealed class GatewayFixture : IAsyncLifetime
{
    public const string SigningKey = "integration-test-signing-key-0123456789abcdef";

    private readonly List<WebApplication> _stubs = [];

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public LogCapture Logs { get; } = new();

    public Uri BaseAddress { get; private set; } = null!;

    public Dictionary<string, string> ServiceUrls { get; } = [];

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "Identity", "Ordering", "Cashier" })
        {
            ServiceUrls[name] = await StartStubAsync(name.ToLowerInvariant());

            // Program đọc cấu hình ngay khi dựng builder, nên truyền qua biến môi trường.
            Environment.SetEnvironmentVariable($"Services__{name}", ServiceUrls[name]);
        }

        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", SigningKey);
        Environment.SetEnvironmentVariable("CORS_ORIGINS", "http://localhost:5173");

        (Factory, BaseAddress) = StartGateway();
    }

    /// <summary>Dựng một gateway trên cổng trống, đọc cấu hình từ biến môi trường tại thời điểm gọi.</summary>
    public (WebApplicationFactory<Program> Factory, Uri Address) StartGateway()
    {
        var factory = new GatewayFactory(Logs);
        factory.UseKestrel(0);
        factory.StartServer();
        var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return (factory, new Uri(address));
    }

    public HttpClient Client(string? token = null)
    {
        var client = new HttpClient { BaseAddress = BaseAddress };
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        return client;
    }

    public static string Token(string role = "Cashier", string key = SigningKey) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtSettings.NameClaim, "tester"),
                new Claim(JwtSettings.RoleClaim, role),
            ]),
        });

    /// <summary>Dịch vụ giả: WebSocket thì nhận rồi đóng; HTTP thì trả "tên đường-dẫn".</summary>
    private async Task<string> StartStubAsync(string name)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.UseWebSockets();
        app.Run(async ctx =>
        {
            if (ctx.WebSockets.IsWebSocketRequest)
            {
                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, name, default);
                return;
            }

            await ctx.Response.WriteAsync($"{name} {ctx.Request.Path}");
        });
        await app.StartAsync();
        _stubs.Add(app);
        return app.Urls.First();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        foreach (var stub in _stubs)
        {
            await stub.DisposeAsync();
        }
    }
}

public sealed class GatewayFactory(ILoggerProvider logs) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(s => s.AddSingleton(logs));
}

/// <summary>Gom mọi dòng log gateway ghi ra (sau bộ lọc của Program) để soi token có lọt không.</summary>
public sealed class LogCapture : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Lines.Enqueue($"{category}: {formatter(state, exception)}");
    }
}
