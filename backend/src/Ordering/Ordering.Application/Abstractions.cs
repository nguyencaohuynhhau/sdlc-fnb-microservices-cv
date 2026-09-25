using Ordering.Domain;

namespace Ordering.Application;

public interface IOrderRepository
{
    /// <summary>Đơn kèm món, được theo dõi để sửa.</summary>
    Task<Order?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Đơn của một ca (chỉ đọc). <paramref name="activeOnly"/>: đơn còn việc — chưa thu tiền, hoặc đã
    /// thu mà bếp chưa làm xong. <paramref name="newestFirst"/>: POS xem mới nhất trước, bếp làm cũ nhất trước.
    /// </summary>
    Task<IReadOnlyList<Order>> ListByShiftAsync(Guid shiftId, OrderStatus? status, bool activeOnly, bool newestFirst, CancellationToken ct);

    void Add(Order order);

    /// <summary>Lưu; trả <c>false</c> khi xmin đã đổi (người khác vừa ghi lên đơn).</summary>
    Task<bool> TrySaveAsync(CancellationToken ct);

    /// <summary>Bỏ mọi thứ đang theo dõi (kể cả outbox chưa lưu) để đọc lại từ DB sau xung đột.</summary>
    void Reset();
}

/// <summary>Ca đang mở theo góc nhìn của ordering — hình chiếu từ sự kiện của cashier.</summary>
public interface ICurrentShift
{
    Task<CurrentShiftView?> GetAsync(CancellationToken ct);
}

public interface IMenuCatalog
{
    /// <summary>Thực đơn cho POS (có cache).</summary>
    Task<IReadOnlyList<MenuItemView>> GetMenuAsync(CancellationToken ct);

    /// <summary>Món theo id, đọc thẳng DB — lúc tạo đơn cần giá và cờ còn hàng mới nhất, không lấy từ cache.</summary>
    Task<IReadOnlyList<MenuItem>> FindActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

/// <summary>Đẩy đơn đã đổi tới màn hình POS/bếp của cùng ca (SignalR).</summary>
public interface IOrderNotifier
{
    Task NotifyAsync(string eventName, OrderView order, CancellationToken ct);
}

public static class OrderEvents
{
    public const string Created = "orderCreated";
    public const string Updated = "orderUpdated";
    public const string ItemStatusChanged = "orderItemStatusChanged";
    public const string Cancelled = "orderCancelled";
}
