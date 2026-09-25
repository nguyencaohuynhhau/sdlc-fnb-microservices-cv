using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel;

namespace Shared.Messaging;

public sealed record KafkaOptions(string BootstrapServers, string ServiceName);

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// DbContext có interceptor outbox + publisher nền. <paramref name="serviceName"/> làm consumer group.
    /// Connection string lấy từ <c>ConnectionStrings:{connectionName}</c>.
    /// </summary>
    public static IServiceCollection AddMessaging<TDb>(
        this IServiceCollection services, IConfiguration config, string serviceName, string connectionName)
        where TDb : DbContext
    {
        var kafka = new KafkaOptions(config.GetConnectionString("Kafka") ?? "localhost:9092", serviceName);
        services.AddSingleton(kafka);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<OutboxSignal>();
        services.AddScoped<OutboxInterceptor>();
        services.AddDbContext<TDb>((sp, o) => o
            .UseNpgsql(config.GetConnectionString(connectionName))
            .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>()));
        services.AddSingleton(_ => new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            // Chống trùng/đảo thứ tự khi producer tự thử lại.
            EnableIdempotence = true,
            Acks = Acks.All,
        }).Build());
        services.AddHostedService<OutboxPublisherHost<TDb>>();
        return services;
    }

    public static IServiceCollection AddConsumer<TDb, TEvent, THandler>(this IServiceCollection services)
        where TDb : DbContext
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        services.AddHostedService<KafkaConsumerHost<TDb, TEvent>>();
        return services;
    }
}
