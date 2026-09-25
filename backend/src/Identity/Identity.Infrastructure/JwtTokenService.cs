using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Application;
using Identity.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Web;

namespace Identity.Infrastructure;

public sealed class JwtTokenService(JwtSettings jwt, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public string CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + ITokenService.AccessTokenLifetime,
            SigningCredentials = new SigningCredentials(jwt.Key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtSettings.NameClaim, user.Username),
                new Claim(JwtSettings.RoleClaim, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        });
    }

    public (string Raw, string Hash) NewRefreshToken()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw));
    }

    public string Hash(string rawRefreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawRefreshToken)));
}
