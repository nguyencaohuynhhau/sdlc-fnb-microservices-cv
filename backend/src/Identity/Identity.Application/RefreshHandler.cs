using Identity.Domain;

namespace Identity.Application;

public sealed class RefreshHandler(IUserRepository users, ITokenService tokens, TimeProvider clock)
{
    public async Task<AuthResult> HandleAsync(string rawRefreshToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var current = await users.FindRefreshTokenAsync(tokens.Hash(rawRefreshToken), ct)
            ?? throw Expired();

        if (current.IsRevoked)
        {
            // Token đã dùng rồi mà có người gửi lại: dấu hiệu bị trộm. Thu hồi mọi token còn sống của người này.
            foreach (var t in await users.ActiveRefreshTokensAsync(current.UserId, now, ct))
            {
                t.Revoke(now);
            }

            await users.TrySaveAsync(ct);
            throw Expired();
        }

        var user = await users.FindAsync(current.UserId, ct);
        if (current.ExpiresAt <= now || user is not { IsActive: true })
        {
            throw Expired();
        }

        var (raw, hash) = tokens.NewRefreshToken();
        var next = new RefreshToken(user.Id, hash, now + ITokenService.RefreshTokenLifetime);
        users.Add(next);
        current.Revoke(now, next.Id);

        // Hai request refresh cùng lúc với cùng token: request thua nhận 401, không được hai token mới.
        if (!await users.TrySaveAsync(ct))
        {
            throw Expired();
        }

        return new AuthResult(tokens.CreateAccessToken(user), raw, user.Role, (int)ITokenService.AccessTokenLifetime.TotalSeconds);
    }

    private static AuthException Expired() => new(401, AuthMessages.SessionExpired);
}
