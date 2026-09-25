using System.Text.Json.Serialization;
using Cashier.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFnbAuth(builder.Configuration);
// "Cash"/"Transfer" dạng chuỗi; số (0, 5, …) bị từ chối — không để enum ngoài miền lọt vào sổ tiền.
builder.Services.AddFnbControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddCashierInfrastructure(builder.Configuration);

var app = builder.Build();

// Migration tự chạy CHỈ ở môi trường dev — không bao giờ đụng database thật.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CashierDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthz();

app.Run();

public partial class Program;
