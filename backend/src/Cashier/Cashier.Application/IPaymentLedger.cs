using Cashier.Domain;

namespace Cashier.Application;

/// <summary>Transaction Postgres đang mở — không commit thì dispose là rollback.</summary>
public interface ICashierTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

/// <summary>Lần gửi trước của cùng key: hash body và nguyên văn phản hồi đã trả.</summary>
public sealed record StoredResponse(string RequestHash, int Status, string Body);

/// <summary>Tổng các bút toán hoàn tất của một ca.</summary>
public sealed record ShiftTotals(int OrderCount, decimal CashTotal, decimal TransferTotal);

public interface IPaymentLedger
{
    Task<ICashierTransaction> BeginAsync(CancellationToken ct);

    /// <summary>
    /// Giữ key cho request này. Trả <c>null</c> khi giữ được (lần đầu). Key đã có → trả lần gửi trước.
    /// Request trùng key đang chạy song song sẽ CHỜ ở đây tới khi request kia commit hoặc rollback.
    /// </summary>
    Task<StoredResponse?> ClaimKeyAsync(Guid key, string endpoint, string requestHash, DateTimeOffset now, CancellationToken ct);

    /// <summary>Ca đang mở, khoá chia sẻ tới hết transaction — đóng ca phải chờ bút toán này xong.</summary>
    Task<Guid?> LockOpenShiftAsync(CancellationToken ct);

    /// <summary>Ghi bút toán và phản hồi cho key (chưa commit).</summary>
    Task RecordAsync(Payment payment, Guid key, string endpoint, int status, string body, CancellationToken ct);

    Task<ShiftTotals> TotalsAsync(Guid shiftId, CancellationToken ct);
}
