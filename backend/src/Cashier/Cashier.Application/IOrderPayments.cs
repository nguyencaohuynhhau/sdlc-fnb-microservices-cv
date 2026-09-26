namespace Cashier.Application;

public enum MarkPaidStatus
{
    Ok,
    TotalMismatch,
    AlreadyPaid,
    OrderCancelled,
    NotFound,
}

/// <param name="ActualTotal">Tổng thật của đơn — chỉ có khi <see cref="MarkPaidStatus.TotalMismatch"/>.</param>
public sealed record MarkPaidResult(MarkPaidStatus Status, decimal? ActualTotal = null);

/// <summary>
/// Cổng đồng bộ duy nhất sang ordering (gRPC <c>MarkPaid</c>). Không kết nối được / quá hạn →
/// ném <see cref="Shared.Kernel.DependencyUnavailableException"/>.
/// </summary>
public interface IOrderPayments
{
    Task<MarkPaidResult> MarkPaidAsync(Guid orderId, decimal expectedTotal, Guid paymentId, CancellationToken ct);
}
