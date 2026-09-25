using Microsoft.EntityFrameworkCore;
using Ordering.Application;
using Ordering.Domain;

namespace Ordering.Infrastructure;

public sealed class OrderRepository(OrderingDbContext db) : IOrderRepository
{
    public Task<Order?> FindAsync(Guid id, CancellationToken ct) =>
        db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Order>> ListByShiftAsync(Guid shiftId, OrderStatus? status, bool newestFirst, CancellationToken ct)
    {
        var q = db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.ShiftId == shiftId);
        if (status is { } s)
        {
            q = q.Where(o => o.Status == s);
        }

        // ponytail: không phân trang — một ca quán cà phê vài trăm đơn; thêm phân trang khi báo cáo cần.
        q = newestFirst ? q.OrderByDescending(o => o.CreatedAt) : q.OrderBy(o => o.CreatedAt);
        return await q.AsSplitQuery().ToListAsync(ct);
    }

    public void Add(Order order) => db.Orders.Add(order);

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
