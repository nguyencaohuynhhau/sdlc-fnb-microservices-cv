using Cashier.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Messaging;

namespace Cashier.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCashierInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddMessaging<CashierDbContext>(config, serviceName: "cashier", connectionName: "Cashier");
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<OpenShiftHandler>();
        services.AddScoped<CloseShiftHandler>();
        return services;
    }
}
