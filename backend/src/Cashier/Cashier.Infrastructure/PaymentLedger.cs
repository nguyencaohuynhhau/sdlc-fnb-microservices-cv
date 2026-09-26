using Cashier.Application;
using Cashier.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cashier.Infrastructure;

/// <summary>
/// Phần "tiền" chạy bằng SQL tường minh: các khoá dòng (<c>ON CONFLICT</c>, <c>FOR SHARE</c>) là thứ
/// giữ đúng tiền khi có request song song, nên để chúng hiện rõ thay vì giấu sau LINQ.
/// </summary>
public sealed class PaymentLedger(CashierDbContext db) : IPaymentLedger
{
    public async Task<ICashierTransaction> BeginAsync(CancellationToken ct) =>
        new Transaction(await db.Database.BeginTransactionAsync(ct));

    public async Task<StoredResponse?> ClaimKeyAsync(Guid key, string endpoint, string requestHash, DateTimeOffset now, CancellationToken ct)
    {
        // Dòng chưa commit của request khác cùng key làm lệnh này chờ; khi request kia commit → 0 dòng,
        // rollback → chèn được và request này chạy như lần đầu.
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO idempotency_keys (key, endpoint, request_hash, created_at)
            VALUES ({key}, {endpoint}, {requestHash}, {now})
            ON CONFLICT DO NOTHING
            """,
            ct);
        if (inserted == 1)
        {
            return null;
        }

        var row = await db.IdempotencyKeys.AsNoTracking().SingleAsync(k => k.Key == key && k.Endpoint == endpoint, ct);

        // Chỉ lần thành công được commit, nên dòng đã có luôn mang phản hồi.
        return new StoredResponse(row.RequestHash, row.ResponseStatus ?? 200, row.ResponseBody ?? "");
    }

    public async Task<Guid?> LockOpenShiftAsync(CancellationToken ct)
    {
        var ids = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM shifts WHERE closed_at IS NULL FOR SHARE")
            .ToListAsync(ct);
        return ids.Count == 0 ? null : ids[0];
    }

    public async Task RecordAsync(Payment payment, Guid key, string endpoint, int status, string body, CancellationToken ct)
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE idempotency_keys SET response_status = {status}, response_body = {body} WHERE key = {key} AND endpoint = {endpoint}",
            ct);
    }

    public async Task<ShiftTotals> TotalsAsync(Guid shiftId, CancellationToken ct)
    {
        var byMethod = await db.Payments
            .Where(p => p.ShiftId == shiftId && p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Count = g.Count(), Sum = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        decimal SumOf(PaymentMethod m) => byMethod.Where(x => x.Method == m).Sum(x => x.Sum);
        return new ShiftTotals(byMethod.Sum(x => x.Count), SumOf(PaymentMethod.Cash), SumOf(PaymentMethod.Transfer));
    }

    private sealed class Transaction(IDbContextTransaction tx) : ICashierTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
