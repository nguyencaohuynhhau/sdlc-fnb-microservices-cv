using Identity.Api.Contracts;
using Identity.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Public: chưa có token thì không thể có token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<IActionResult> Login(LoginRequest req, [FromServices] LoginHandler handler, CancellationToken ct) =>
        Run(() => handler.HandleAsync(req.Username, req.Password, ct));

    /// <summary>Public: dùng refresh token thay cho access token đã hết hạn.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public Task<IActionResult> Refresh(RefreshRequest req, [FromServices] RefreshHandler handler, CancellationToken ct) =>
        Run(() => handler.HandleAsync(req.RefreshToken, ct));

    private async Task<IActionResult> Run(Func<Task<AuthResult>> action)
    {
        try
        {
            var r = await action();
            return Ok(new AuthResponse(r.AccessToken, r.RefreshToken, r.Role.ToString(), r.ExpiresIn));
        }
        catch (AuthException ex)
        {
            return Problem(detail: ex.Message, statusCode: ex.Status);
        }
    }
}
