using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ordering.Application;

namespace Ordering.Api;

/// <summary>Mọi response trả một đơn kèm <c>ETag: "&lt;xmin&gt;"</c>.</summary>
public sealed class ETagFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext ctx)
    {
        if (ctx.Result is ObjectResult { Value: OrderView order })
        {
            ctx.HttpContext.Response.Headers.ETag = $"\"{order.Version}\"";
        }
    }

    public void OnResultExecuted(ResultExecutedContext ctx)
    {
    }
}

/// <summary>
/// Lệnh ghi lên đơn đã có phải gửi <c>If-Match</c> — thiếu thì 428, để client cũ quên gửi không
/// âm thầm ghi đè thay đổi của người khác.
/// </summary>
public sealed class RequireIfMatchAttribute : ActionFilterAttribute
{
    private const string Key = "if-match-version";

    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        var raw = ctx.HttpContext.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            ctx.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status428PreconditionRequired,
                Detail = "Thiếu phiên bản đơn. Tải lại rồi thử lại nhé.",
            })
            { StatusCode = StatusCodes.Status428PreconditionRequired, ContentTypes = { "application/problem+json" } };
            return;
        }

        // Chấp nhận "12", 12 và W/"12". Không đọc được thì coi như lệch phiên bản (null ≠ mọi xmin).
        var value = raw.Trim().TrimStart('W', '/').Trim('"');
        ctx.HttpContext.Items[Key] = uint.TryParse(value, out var v) ? v : null;
    }

    public static uint? ExpectedVersion(HttpContext http) => http.Items[Key] as uint?;
}
