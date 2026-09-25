using Cashier.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ordering.Domain;
using Ordering.Infrastructure;
using Testcontainers.PostgreSql;

namespace Seeder.IntegrationTests;

/// <summary>Một Postgres cho cả lớp — các test cùng chạy seed trên đó, đúng tinh thần "chạy lại bao lần cũng được".</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Pg { get; } = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => Pg.StartAsync();

    public Task DisposeAsync() => Pg.DisposeAsync().AsTask();
}

[Trait("Category", "Integration")]
public sealed class SeederTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private string Conn(string db) => new NpgsqlConnectionStringBuilder(fx.Pg.GetConnectionString()) { Database = db }.ConnectionString;

    [Fact]
    public async Task SeedTwice_SameSummary_NoDuplicates()
    {
        var first = await DemoData.SeedAsync(Conn, "demo-password", DateTimeOffset.UtcNow);
        var second = await DemoData.SeedAsync(Conn, "demo-password", DateTimeOffset.UtcNow.AddMinutes(5));

        first.Should().Be("Seeded: 3 users, 20 menu items, 1 closed shift, 1 open shift, 3 open orders");
        second.Should().Be(first);
    }

    [Fact]
    public async Task OpenShift_IsVisibleToOrdering_WithItsOrders()
    {
        await DemoData.SeedAsync(Conn, "demo-password", DateTimeOffset.UtcNow);

        await using var cashier = new CashierDbContext(new DbContextOptionsBuilder<CashierDbContext>().UseNpgsql(Conn("fnb_cashier")).Options);
        await using var ordering = new OrderingDbContext(new DbContextOptionsBuilder<OrderingDbContext>().UseNpgsql(Conn("fnb_ordering")).Options);
        var open = await cashier.Shifts.SingleAsync(s => s.ClosedAt == null);
        var current = await new CurrentShiftReader(ordering).GetAsync(default);

        current!.ShiftId.Should().Be(open.Id, "POS phải thấy đúng ca đang mở của cashier mà không cần Kafka");
        (await ordering.Orders.CountAsync(o => o.ShiftId == open.Id && o.Status == OrderStatus.Open)).Should().Be(3);
        (await ordering.MenuItems.SingleAsync(m => m.Name == "Bánh flan")).IsAvailable.Should().BeFalse();
    }

    /// <summary>Tiền là decimal(18,2) ở DB (spec §3) — soi schema sau migrate, bắt cả cấu hình EF lẫn migration thiếu.</summary>
    [Theory]
    [InlineData("fnb_ordering", 3)]
    [InlineData("fnb_cashier", 4)]
    public async Task MoneyColumns_AreNumeric18_2(string db, int moneyColumns)
    {
        await DemoData.SeedAsync(Conn, "demo-password", DateTimeOffset.UtcNow);

        await using var conn = new NpgsqlConnection(Conn(db));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT table_name || '.' || column_name || ' ' || numeric_precision || ',' || numeric_scale FROM information_schema.columns WHERE table_schema = 'public' AND data_type = 'numeric'",
            conn);
        var columns = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }
        }

        columns.Should().HaveCount(moneyColumns).And.OnlyContain(c => c.EndsWith(" 18,2"));
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("development", false)]
    [InlineData(null, false)]
    public void OnlyDevelopmentIsAllowed(string? environment, bool allowed) =>
        DemoData.IsAllowed(environment).Should().Be(allowed);
}
