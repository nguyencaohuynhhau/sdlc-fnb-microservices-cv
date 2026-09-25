using Cashier.Application;
using Cashier.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cashier.Infrastructure;

public sealed class ShiftRepository(CashierDbContext db) : IShiftRepository
{
    public Task<Shift?> CurrentAsync(CancellationToken ct) => db.Shifts.SingleOrDefaultAsync(s => s.ClosedAt == null, ct);

    public Task<Shift?> FindAsync(Guid id, CancellationToken ct) => db.Shifts.FindAsync([id], ct).AsTask();

    public void Add(Shift shift) => db.Shifts.Add(shift);

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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }
}
