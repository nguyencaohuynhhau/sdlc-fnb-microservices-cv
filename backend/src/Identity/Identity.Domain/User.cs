using Shared.Kernel;

namespace Identity.Domain;

public enum Role
{
    Owner,
    Cashier,
    Kitchen,
}

public sealed class User : Entity
{
    private User()
    {
    }

    public User(string username, Role role)
    {
        Username = Normalize(username);
        Role = role;
        IsActive = true;
    }

    public string Username { get; private set; } = "";

    public string PasswordHash { get; private set; } = "";

    public Role Role { get; private set; }

    public bool IsActive { get; private set; }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    /// <summary>Tên đăng nhập không phân biệt hoa thường — "Cashier" và "cashier" là một người.</summary>
    public static string Normalize(string username) => username.Trim().ToLowerInvariant();
}
