using Shared.Kernel;

namespace Identity.Domain;

/// <summary>
/// Refresh token chỉ lưu dạng băm SHA-256; token thô chỉ tồn tại trong phản hồi gửi cho client.
/// Mỗi lần dùng là một lần xoay vòng: token cũ bị thu hồi, trỏ sang token mới.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = "";

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedBy { get; private set; }

    /// <summary>Phiên bản dòng (xmin) — hai request refresh cùng một token thì chỉ một cái thắng.</summary>
    public uint Version { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public void Revoke(DateTimeOffset now, Guid? replacedBy = null)
    {
        RevokedAt ??= now;
        ReplacedBy ??= replacedBy;
    }
}
