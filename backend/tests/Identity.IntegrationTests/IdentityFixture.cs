using Identity.Domain;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Identity.IntegrationTests;

/// <summary>Identity chạy thật trên Postgres + Redis trong container — không mock tầng lưu trữ.</summary>
public sealed class IdentityFixture : IAsyncLifetime
{
    public const string Password = "Correct#Pass1";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(), _redis.StartAsync());

        // Program đọc cấu hình ngay khi dựng builder, nên truyền qua biến môi trường.
        Environment.SetEnvironmentVariable("ConnectionStrings__Identity", _pg.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", "integration-test-signing-key-0123456789abcdef");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        Factory = new WebApplicationFactory<Program>();
        _ = Factory.Server; // khởi động host → chạy migration

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = new PasswordHasher<User>();
        foreach (var (name, role) in new[] { ("cashier", Role.Cashier), ("owner", Role.Owner), ("lockme", Role.Cashier), ("rotator", Role.Cashier) })
        {
            var user = new User(name, role);
            user.SetPasswordHash(hasher.HashPassword(user, Password));
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
