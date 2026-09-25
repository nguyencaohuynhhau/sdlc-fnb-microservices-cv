using Identity.Domain;

namespace Identity.Application;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);

    Task<User?> FindAsync(Guid id, CancellationToken ct);

    Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct);

    Task<IReadOnlyList<RefreshToken>> ActiveRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct);

    void Add(RefreshToken token);

    /// <summary>Lưu thay đổi; trả <c>false</c> khi dòng đã bị request khác sửa trước (xung đột xmin).</summary>
    Task<bool> TrySaveAsync(CancellationToken ct);
}

public interface ITokenService
{
    static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(60);

    static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromHours(12);

    /// <summary>Access token JWT, sống <see cref="AccessTokenLifetime"/>.</summary>
    string CreateAccessToken(User user);

    /// <summary>Sinh refresh token ngẫu nhiên; trả cả bản thô (gửi client) và bản băm (lưu DB).</summary>
    (string Raw, string Hash) NewRefreshToken();

    string Hash(string rawRefreshToken);
}

/// <summary>Đếm lần đăng nhập sai theo tên đăng nhập, trong cửa sổ 15 phút.</summary>
public interface ILoginAttemptStore
{
    Task<long> FailureCountAsync(string username);

    Task RecordFailureAsync(string username);

    Task ResetAsync(string username);
}

public sealed record AuthResult(string AccessToken, string RefreshToken, Role Role, int ExpiresIn);

/// <summary>Đăng nhập/refresh thất bại. <see cref="Status"/> là mã HTTP (401 hoặc 423).</summary>
public sealed class AuthException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class AuthMessages
{
    public const string InvalidCredentials = "Tên đăng nhập hoặc mật khẩu không đúng.";
    public const string Locked = "Tài khoản tạm khoá 15 phút vì đăng nhập sai quá nhiều lần.";
    public const string SessionExpired = "Phiên đăng nhập đã hết hạn. Đăng nhập lại nhé.";
}
