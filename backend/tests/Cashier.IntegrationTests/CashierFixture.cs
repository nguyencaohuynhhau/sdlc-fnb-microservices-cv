using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Web;
using Testcontainers.PostgreSql;

namespace Cashier.IntegrationTests;

/// <summary>Cashier chạy thật trên Postgres trong container. Kafka trỏ vào cổng chết: test chỉ soi outbox.</summary>
public sealed class CashierFixture : IAsyncLifetime
{
    private const string SigningKey = "integration-test-signing-key-0123456789abcdef";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        // Program đọc cấu hình ngay khi dựng builder, nên truyền qua biến môi trường.
        Environment.SetEnvironmentVariable("ConnectionStrings__Cashier", _pg.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Kafka", "127.0.0.1:1");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", SigningKey);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        Factory = new WebApplicationFactory<Program>();
        _ = Factory.Server; // khởi động host → chạy migration
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
        await Factory.DisposeAsync();
        await _pg.DisposeAsync();
    }
}
