using Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFnbAuth(builder.Configuration);
builder.Services.AddFnbControllers();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();

// Migration tự chạy CHỈ ở môi trường dev — không bao giờ đụng database thật.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthz();

app.Run();

public partial class Program;
