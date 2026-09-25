using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cashier.Application;
using Cashier.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Web;
using Testcontainers.PostgreSql;

namespace Cashier.IntegrationTests;

/// <summary>Một fixture cho mọi lớp test: fixture truyền cấu hình qua biến môi trường của process, hai fixture song song sẽ giẫm lên nhau.</summary>
[CollectionDefinition(Name)]
public sealed class CashierCollection : ICollectionFixture<CashierFixture>
{
    public const string Name = "cashier";
}

/// <summary>Cashier chạy thật trên Postgres trong container. Kafka trỏ vào cổng chết: test chỉ soi outbox.</summary>
public sealed class CashierFixture : IAsyncLifetime
{
    private const string SigningKey = "integration-test-signing-key-0123456789abcdef";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();

    private readonly WebApplicationFactory<Program> _root = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        // Program đọc cấu hình ngay khi dựng builder, nên truyền qua biến môi trường.
        Environment.SetEnvironmentVariable("ConnectionStrings__Cashier", _pg.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Kafka", "127.0.0.1:1");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", SigningKey);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        Factory = _root.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IOrderPayments>(Orders)));
        _ = Factory.Server; // khởi động host → chạy migration
    }

    /// <summary>Thay gRPC sang ordering.</summary>
    public FakeOrderPayments Orders { get; } = new();

    /// <summary>Đóng mọi ca đang mở rồi mở một ca mới qua API.</summary>
    public async Task<Guid> OpenFreshShiftAsync(HttpClient client, decimal openingFloat = 0m)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CashierDbContext>().Shifts.Where(s => s.ClosedAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ClosedAt, DateTimeOffset.UtcNow));
        }

        var res = await client.PostAsJsonAsync("/api/shifts/open", new { openingFloat });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public async Task<T> WithDbAsync<T>(Func<CashierDbContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CashierDbContext>());
    }

    /// <summary>HttpClient mang access token hợp lệ của một vai trò — thay cho việc gọi identity.</summary>
    public HttpClient ClientAs(string role, string username = "tester")
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtSettings.NameClaim, username),
                new Claim(JwtSettings.RoleClaim, role),
            ]),
        });
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        await _root.DisposeAsync(); // kéo theo factory con
        await _pg.DisposeAsync();
    }
}
