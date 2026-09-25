using Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Identity.Application;

public sealed class LoginHandler(
    IUserRepository users,
    ITokenService tokens,
    ILoginAttemptStore attempts,
    IPasswordHasher<User> hasher,
    TimeProvider clock)
{
    public const int MaxFailures = 10;

    // Băm giả để người dùng không tồn tại vẫn tốn thời gian như sai mật khẩu — chống dò tài khoản qua độ trễ.
    private static readonly User DummyUser = new("dummy", Role.Cashier);
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(DummyUser, "dummy-password");

    public async Task<AuthResult> HandleAsync(string username, string password, CancellationToken ct)
    {
        var name = User.Normalize(username);
        if (await attempts.FailureCountAsync(name) >= MaxFailures)
        {
            throw new AuthException(423, AuthMessages.Locked);
        }

        var user = await users.FindByUsernameAsync(name, ct);
        bool verified;
        if (user is null)
        {
            hasher.VerifyHashedPassword(DummyUser, DummyHash, password);
            verified = false;
        }
        else
        {
            verified = user.IsActive
                && hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
        }

        if (!verified)
        {
            // Không phân biệt "sai mật khẩu" với "không có tài khoản".
            await attempts.RecordFailureAsync(name);
            throw new AuthException(401, AuthMessages.InvalidCredentials);
        }

        await attempts.ResetAsync(name);
        var (raw, hash) = tokens.NewRefreshToken();
        users.Add(new RefreshToken(user!.Id, hash, clock.GetUtcNow() + ITokenService.RefreshTokenLifetime));
        await users.TrySaveAsync(ct);
        return new AuthResult(tokens.CreateAccessToken(user), raw, user.Role, (int)ITokenService.AccessTokenLifetime.TotalSeconds);
    }
}
