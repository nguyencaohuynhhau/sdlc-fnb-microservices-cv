using Cashier.Domain;

namespace Cashier.Application;

public interface IShiftRepository
{
    Task<Shift?> CurrentAsync(CancellationToken ct);

    Task<Shift?> FindAsync(Guid id, CancellationToken ct);

    void Add(Shift shift);

    /// <summary>
    /// Lưu thay đổi; trả <c>false</c> khi DB từ chối vì request khác đã thắng trước
    /// (vi phạm "chỉ một ca mở" hoặc xung đột xmin khi đóng ca).
    /// </summary>
    Task<bool> TrySaveAsync(CancellationToken ct);
}
