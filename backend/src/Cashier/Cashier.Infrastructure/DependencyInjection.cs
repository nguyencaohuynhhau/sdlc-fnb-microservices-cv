using Cashier.Application;
using Fnb.Ordering.V1;
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
        services.AddScoped<IPaymentLedger, PaymentLedger>();
        services.AddScoped<PayOrderHandler>();

        services.AddHttpContextAccessor();
        services.AddTransient<ForwardAuthHandler>();
        services.AddGrpcClient<OrderPayments.OrderPaymentsClient>(o =>
                o.Address = new Uri(config["Services:OrderingGrpc"] ?? "http://localhost:8092"))
            .AddHttpMessageHandler<ForwardAuthHandler>();
        services.AddScoped<IOrderPayments, OrderPaymentsClient>();
        return services;
    }
}
