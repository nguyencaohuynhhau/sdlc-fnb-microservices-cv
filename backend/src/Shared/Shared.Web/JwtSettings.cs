using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Web;

/// <summary>Tham số JWT dùng chung cho identity (ký) và mọi dịch vụ + gateway (kiểm).</summary>
public sealed class JwtSettings
{
    public const string Issuer = "fnb-identity";
    public const string Audience = "fnb";
    public const string RoleClaim = "role";
    public const string NameClaim = "unique_name";

    public required SymmetricSecurityKey Key { get; init; }

    /// <summary>Đọc khoá từ biến môi trường <c>JWT_SIGNING_KEY</c>; thiếu hoặc ngắn hơn 32 byte thì dừng khởi động.</summary>
    public static JwtSettings From(IConfiguration config)
    {
        var raw = config["JWT_SIGNING_KEY"];
        if (string.IsNullOrEmpty(raw) || Encoding.UTF8.GetByteCount(raw) < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY phải được đặt và dài tối thiểu 32 byte.");
        }

        return new JwtSettings { Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(raw)) };
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = Key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RoleClaimType = RoleClaim,
        NameClaimType = NameClaim,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
