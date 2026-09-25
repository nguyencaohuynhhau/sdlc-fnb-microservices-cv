using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application;
using Shared.Messaging;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Mọi thứ trừ <see cref="IOrderNotifier"/> — cái đó cần Hub nên nằm ở tầng Api.</summary>
    public static IServiceCollection AddOrderingInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddMessaging<OrderingDbContext>(config, serviceName: "ordering", connectionName: "Ordering");
        services.AddConsumer<OrderingDbContext, ShiftOpened, ShiftEventsConsumer>();
        services.AddConsumer<OrderingDbContext, ShiftClosed, ShiftEventsConsumer>();
        services.AddStackExchangeRedisCache(o => o.Configuration = config.GetConnectionString("Redis") ?? "localhost:6379");
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICurrentShift, CurrentShiftReader>();
        services.AddScoped<IMenuCatalog, MenuCatalog>();
        services.AddScoped<OrderService>();
        return services;
    }
}
