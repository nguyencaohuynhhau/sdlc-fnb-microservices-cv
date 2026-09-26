using Cashier.Domain;
using Shared.Kernel;

namespace Cashier.Application;

/// <summary>Tổng kết ca trả cho màn đóng ca: đối chiếu két + doanh thu.</summary>
public sealed record ShiftSummary(Shift Shift, ShiftTotals Totals);

public sealed class CloseShiftHandler(IShiftRepository shifts, IPaymentLedger ledger, TimeProvider clock)
{
    public async Task<ShiftSummary> HandleAsync(Guid id, decimal countedCash, CancellationToken ct)
    {
        await using var tx = await ledger.BeginAsync(ct);

        // FOR UPDATE: chờ mọi lần thu tiền đang giữ ca (FOR SHARE) commit xong rồi mới cộng — không
        // bút toán nào lọt vào ca sau khi đã chốt số.
        var shift = await shifts.LockAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy ca.");
        var totals = await ledger.TotalsAsync(id, ct);
        shift.Close(countedCash, totals.CashTotal, totals.TransferTotal, clock.GetUtcNow());

        // Người khác vừa đóng đúng ca này (xmin đổi) — kết quả với người dùng là như nhau.
        if (!await shifts.TrySaveAsync(ct))
        {
            throw new DomainException(Shift.AlreadyClosedMessage);
        }

        await tx.CommitAsync(ct);
        return new ShiftSummary(shift, totals);
    }
}
