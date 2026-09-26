using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Ordering.Application;
using Ordering.Domain;
using Ordering.Infrastructure;
using Shared.Web;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Ordering.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class OrderingCollection : ICollectionFixture<OrderingFixture>
{
    public const string Name = "ordering";
}

/// <summary>Ordering chạy thật trên Postgres + Redis + Kafka trong container. Các lớp test dùng chung, chạy lần lượt.</summary>
public sealed class OrderingFixture : IAsyncLifetime
{
    private const string SigningKey = "integration-test-signing-key-0123456789abcdef";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder("apache/kafka:3.9.1").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string KafkaBootstrap => _kafka.GetBootstrapAddress();

    public MenuItem Latte { get; } = new("Latte", 45_000m);

    public MenuItem Croissant { get; } = new("Croissant", 30_000m);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(), _redis.StartAsync(), _kafka.StartAsync());

        // Program đọc cấu hình ngay khi dựng builder, nên truyền qua biến môi trường.
        Environment.SetEnvironmentVariable("ConnectionStrings__Ordering", _pg.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Kafka", KafkaBootstrap);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", SigningKey);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        Factory = new WebApplicationFactory<Program>();
        _ = Factory.Server; // khởi động host → chạy migration

        await WithDbAsync(db =>
        {
            db.MenuItems.AddRange(Latte, Croissant);
            return db.SaveChangesAsync();
        });
    }

    /// <summary>Connection string tới một database riêng trong cùng container (test cần DB cách ly).</summary>
    public string ConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { Database = database }.ToString();

    public async Task WithDbAsync(Func<OrderingDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<OrderingDbContext>());
    }

    /// <summary>Đặt hình chiếu ca như thể vừa nhận ShiftOpened — cho test không cần đi qua Kafka.</summary>
    public async Task<Guid> OpenShiftAsync()
    {
        await CloseAllShiftsAsync();
        var id = Guid.NewGuid();
        await WithDbAsync(db =>
        {
            db.KnownShifts.Add(new KnownShift { ShiftId = id, OpenedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        return id;
    }

    public Task CloseAllShiftsAsync() => WithDbAsync(db =>
        db.KnownShifts.Where(s => s.ClosedAt == null).ExecuteUpdateAsync(u => u.SetProperty(s => s.ClosedAt, DateTimeOffset.UtcNow)));

    public async Task<OrderView> CreateOrderAsync(HttpClient client, params (MenuItem Item, int Qty)[] lines)
    {
        var res = await client.PostAsJsonAsync("/api/orders", new { items = lines.Select(l => new { menuItemId = l.Item.Id, qty = l.Qty }) });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<OrderView>())!;
    }

    /// <summary>HttpClient mang access token hợp lệ của một vai trò — thay cho việc gọi identity.</summary>
    public HttpClient ClientAs(string role)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(role));
        return client;
    }

    public static string TokenFor(string role) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtSettings.NameClaim, role.ToLowerInvariant()),
                new Claim(JwtSettings.RoleClaim, role),
            ]),
        });

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask());
    }
}
