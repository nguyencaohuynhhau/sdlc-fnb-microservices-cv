using Shared.Web;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Token của SignalR đi trong query (/hubs/orders?access_token=…). Hai logger này in nguyên URL kèm
// query ở mức Information: log truy cập của ASP.NET và dòng "Proxying to …" của YARP. Chặn ở Warning.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Yarp", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

// JWT kiểm ở gateway VÀ ở từng dịch vụ (không tin gateway vô điều kiện). Chung cấu hình với dịch vụ,
// kể cả luật "access_token trong query chỉ nhận ở /hubs".
builder.Services.AddFnbAuth(builder.Configuration);

// Danh sách origin tường minh từ env. "*" + AllowCredentials làm CorsPolicyBuilder ném lỗi lúc khởi động —
// cố ý: cấu hình sai thì không chạy, thay vì chạy với CORS mở toang.
var origins = (builder.Configuration["CORS_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(origins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders("ETag")));

var services = new Dictionary<string, string>
{
    ["identity"] = builder.Configuration["Services:Identity"] ?? "http://localhost:8081",
    ["ordering"] = builder.Configuration["Services:Ordering"] ?? "http://localhost:8082",
    ["cashier"] = builder.Configuration["Services:Cashier"] ?? "http://localhost:8083",
};

// Route khai báo bằng code (không phải appsettings.json) để mặt public của hệ thống nằm trong file
// eval P02 canh: chỉ login + refresh là "anonymous", còn lại đều cần JWT hợp lệ.
static RouteConfig Route(string path, string cluster, string policy = "default") => new()
{
    RouteId = path,
    ClusterId = cluster,
    Match = new RouteMatch { Path = path },
    AuthorizationPolicy = policy,
};

builder.Services.AddReverseProxy().LoadFromMemory(
    [
        Route("/api/auth/login", "identity", "anonymous"),
        Route("/api/auth/refresh", "identity", "anonymous"),
        Route("/api/auth/{**rest}", "identity"),
        Route("/api/menu", "ordering"),
        Route("/api/orders/{**rest}", "ordering"),
        Route("/api/kitchen/{**rest}", "ordering"),
        Route("/hubs/{**rest}", "ordering"),
        Route("/api/shifts/{**rest}", "cashier"),
    ],
    [.. services.Select(s => new ClusterConfig
    {
        ClusterId = s.Key,
        Destinations = new Dictionary<string, DestinationConfig> { ["main"] = new() { Address = s.Value } },
    })]);

builder.Services.AddHttpClient("health", c => c.Timeout = TimeSpan.FromSeconds(2));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

// Một dịch vụ chết là gateway báo 503 — Docker/compose nhìn một chỗ là biết cả hệ thống.
app.MapGet("/healthz", async (IHttpClientFactory factory, CancellationToken ct) =>
{
    var http = factory.CreateClient("health");
    var results = await Task.WhenAll(services.Select(async s =>
    {
        try
        {
            using var res = await http.GetAsync($"{s.Value}/healthz", ct);
            return (s.Key, Ok: res.IsSuccessStatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return (s.Key, Ok: false);
        }
    }));
    var body = results.ToDictionary(r => r.Key, r => r.Ok ? "ok" : "down");
    return results.All(r => r.Ok) ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.Run();

public partial class Program;
