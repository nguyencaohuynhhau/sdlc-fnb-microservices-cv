using Confluent.Kafka;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ordering.Domain;
using Ordering.Infrastructure;
using Shared.Kernel;
using Shared.Messaging;

namespace Ordering.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(OrderingCollection.Name)]
public sealed class OutboxTests(OrderingFixture fx)
{
    /// <summary>Dịch vụ "ordering" dựng từ đầu trên một database riêng — mỗi lần gọi là một lần khởi động.</summary>
    private ServiceProvider Boot() => new ServiceCollection()
        .AddLogging()
        .AddMessaging<OrderingDbContext>(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Ordering"] = fx.ConnectionString("outbox_restart"),
                ["ConnectionStrings:Kafka"] = fx.KafkaBootstrap,
            }).Build(),
            serviceName: "ordering-outbox-test",
            connectionName: "Ordering")
        .BuildServiceProvider();

    [Fact]
    public async Task Outbox_CrashBeforePublish_PublishesOnRestart()
    {
        Guid orderId;

        // Lần chạy 1: huỷ đơn → OrderCancelled vào outbox cùng transaction, rồi "sập" trước khi publisher kịp đẩy.
        await using (var crashed = Boot())
        {
            using var scope = crashed.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            await db.Database.MigrateAsync();
            var order = Order.Create(Guid.NewGuid(), [(new MenuItem("Latte", 45_000m), 1)], DateTimeOffset.UtcNow);
            order.Cancel(DateTimeOffset.UtcNow);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        // Lần chạy 2: khởi động lại — publisher phải tự quét và đẩy dòng còn sót, không ai rung chuông.
        await using var restarted = Boot();
        var publisher = restarted.GetServices<IHostedService>().OfType<OutboxPublisherHost<OrderingDbContext>>().Single();
        await publisher.StartAsync(default);
        try
        {
            var published = await WaitAsync(async () =>
            {
                using var scope = restarted.CreateScope();
                var row = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>()
                    .Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Key == orderId.ToString());
                return row.PublishedAt is not null;
            });
            published.Should().BeTrue("publisher phải đẩy message còn sót trong outbox sau khi khởi động lại");

            ConsumeKey(Topics.OrderCancelled, orderId.ToString()).Should().NotBeNull();
        }
        finally
        {
            await publisher.StopAsync(default);
        }
    }

    private static async Task<bool> WaitAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }

    private string? ConsumeKey(string topic, string key)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = fx.KafkaBootstrap,
            GroupId = $"test-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(topic);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (consumer.Consume(TimeSpan.FromSeconds(1)) is { } r && r.Message.Key == key)
            {
                return r.Message.Value;
            }
        }

        return null;
    }
}
