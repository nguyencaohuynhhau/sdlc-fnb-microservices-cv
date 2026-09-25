using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ordering.Api;
using Ordering.Api.Hubs;
using Ordering.Application;
using Ordering.Infrastructure;
using Shared.Web;

var builder = WebApplication.CreateBuilder(args);

// Log "Request starting …" in cả query string, mà /hubs mang access_token trên query — tắt ở mức Information.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

builder.Services.AddFnbAuth(builder.Configuration);
builder.Services.AddFnbControllers()
    .AddMvcOptions(o => o.Filters.Add<ETagFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddOrderingInfrastructure(builder.Configuration);
builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();

var app = builder.Build();

// Migration tự chạy CHỈ ở môi trường dev — không bao giờ đụng database thật.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");
app.MapHealthz();

app.Run();

public partial class Program;
