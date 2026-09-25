using Identity.Application;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> FindAsync(Guid id, CancellationToken ct) => db.Users.FindAsync([id], ct).AsTask();

    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> ActiveRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now).ToListAsync(ct);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
