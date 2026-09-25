using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel;
using Testcontainers.PostgreSql;

namespace Shared.Messaging.Tests;

[Topic("fnb.test.thing-happened.v1")]
public sealed record ThingHappened(Guid ThingId) : IntegrationEvent
{
    public override string PartitionKey => ThingId.ToString();
}

public sealed class Thing : Entity
{
    public string Name { get; set; } = "";

    public void Touch() => Raise(new ThingHappened(Id));
}

public sealed class TestDb(DbContextOptions<TestDb> o) : DbContext(o)
{
    public DbSet<Thing> Things => Set<Thing>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Thing>().ToTable("things").Ignore(x => x.Events);
        b.AddMessaging().UseSnakeCaseNames();
    }
}

public sealed class CountingHandler(TestDb db) : IIntegrationEventHandler<ThingHappened>
{
    public static int Calls;

    public Task HandleAsync(ThingHappened e, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        db.Things.Add(new Thing { Name = $"from-{e.ThingId}" });
        return Task.CompletedTask;
    }
}

[Trait("Category", "Integration")]
public sealed class InboxDedupTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();
    private ServiceProvider _sp = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _sp = new ServiceCollection()
            .AddSingleton<OutboxSignal>()
            .AddScoped<OutboxInterceptor>()
            .AddDbContext<TestDb>((sp, o) => o.UseNpgsql(_pg.GetConnectionString()).AddInterceptors(sp.GetRequiredService<OutboxInterceptor>()))
            .AddScoped<IIntegrationEventHandler<ThingHappened>, CountingHandler>()
            .BuildServiceProvider();
        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestDb>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        await _pg.DisposeAsync();
    }

    [Fact]
    public async Task SameMessageId_DeliveredThreeTimes_HandlerRunsOnce()
    {
        var e = new ThingHappened(Guid.NewGuid());
        var scopes = _sp.GetRequiredService<IServiceScopeFactory>();

        var results = new List<bool>();
        for (var i = 0; i < 3; i++)
        {
            results.Add(await KafkaConsumerHost<TestDb, ThingHappened>.ProcessAsync(scopes, e, CancellationToken.None));
        }

        results.Should().Equal(true, false, false);
        CountingHandler.Calls.Should().Be(1);
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDb>();
        (await db.Things.CountAsync(t => t.Name == $"from-{e.ThingId}")).Should().Be(1);
        (await db.Set<InboxMessage>().CountAsync(m => m.MessageId == e.MessageId)).Should().Be(1);
    }

    [Fact]
    public async Task RaisedEvent_WrittenToOutbox_InSameSaveChanges()
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDb>();
        var thing = new Thing { Name = "outbox" };
        thing.Touch();
        db.Things.Add(thing);

        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync(m => m.Key == thing.Id.ToString());
        row.Topic.Should().Be("fnb.test.thing-happened.v1");
        row.PublishedAt.Should().BeNull();
        row.Payload.Should().Contain(thing.Id.ToString());
        thing.Events.Should().BeEmpty();
    }
}
