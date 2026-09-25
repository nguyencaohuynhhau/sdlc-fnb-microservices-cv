using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Contracts;

public sealed record LoginRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public sealed record RefreshRequest([Required, StringLength(200)] string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, string Role, int ExpiresIn);
