using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel;

namespace Shared.Web;

public static class WebExtensions
{
    /// <summary>
    /// JWT + chính sách mặc định: MỌI endpoint cần đăng nhập, trừ chỗ ghi [AllowAnonymous] tường minh
    /// (danh sách được eval P02 canh).
    /// </summary>
    public static IServiceCollection AddFnbAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = JwtSettings.From(config);
        services.AddSingleton(jwt);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = jwt.ValidationParameters();
                o.Events = new JwtBearerEvents
                {
                    // Trình duyệt không đặt được header cho WebSocket nên SignalR gửi token qua query.
                    // Chỉ chấp nhận cách này ở /hubs để token không lọt vào URL của API thường.
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")
                            && ctx.Request.Query["access_token"] is { Count: > 0 } token)
                        {
                            ctx.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    /// <summary>Controller + ProblemDetails với <c>detail</c> tiếng Việt cho lỗi validation và lỗi nghiệp vụ.</summary>
    public static IMvcBuilder AddFnbControllers(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();
        return services.AddControllers().ConfigureApiBehaviorOptions(o =>
            o.InvalidModelStateResponseFactory = ctx =>
            {
                var problem = new ValidationProblemDetails(ctx.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Dữ liệu gửi lên không hợp lệ.",
                };
                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            });
    }

    /// <summary>Probe hạ tầng cho Docker healthcheck và gateway — public vì probe không có token.</summary>
    public static IEndpointConventionBuilder MapHealthz(this IEndpointRouteBuilder app) =>
        app.MapGet("/healthz", () => "ok").AllowAnonymous();

    /// <summary>Trả ProblemDetails 409/404/400/422/503 với <c>detail</c> = câu tiếng Việt trong exception.</summary>
    private sealed class DomainExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
        {
            if (ex is not DomainException domain)
            {
                return false;
            }

            var status = domain switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                InvalidRequestException => StatusCodes.Status400BadRequest,
                UnprocessableRequestException => StatusCodes.Status422UnprocessableEntity,
                DependencyUnavailableException => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status409Conflict,
            };
            http.Response.StatusCode = status;
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = { Status = status, Detail = domain.Message },
            });
        }
    }
}
