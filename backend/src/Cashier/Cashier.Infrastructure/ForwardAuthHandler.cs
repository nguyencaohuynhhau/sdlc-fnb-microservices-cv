using Microsoft.AspNetCore.Http;

namespace Cashier.Infrastructure;

/// <summary>
/// Gọi ordering bằng chính JWT của thu ngân: ordering kiểm vai trò như mọi request khác, không có
/// "tài khoản dịch vụ" nào được quyền hơn người đang bấm nút.
/// </summary>
public sealed class ForwardAuthHandler(IHttpContextAccessor http) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = http.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth))
        {
            request.Headers.TryAddWithoutValidation("Authorization", auth);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
