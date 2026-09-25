using Shared.Kernel;

namespace Ordering.Domain;

public enum OrderStatus
{
    Open,
    Paid,
    Cancelled,
}

public enum OrderItemStatus
{
    Pending,
    Preparing,
    Done,
    Cancelled,
}

/// <summary>
/// Đơn hàng — gốc aggregate. Thu ngân (thêm/huỷ món) và bếp (đổi trạng thái món) cùng ghi lên
/// đơn này, nên MỌI thay đổi đều chạm dòng <c>orders</c> (qua <see cref="UpdatedAt"/>) để xmin đổi
/// và người ghi sau cầm phiên bản cũ bị từ chối.
/// </summary>
public sealed class Order : Entity
{
    public const string NoOpenShiftMessage = "Chưa mở ca làm việc. Mở ca trước khi nhận đơn.";
    public const string NotFoundMessage = "Không tìm thấy đơn.";
    public const string StaleVersionMessage = "Đơn vừa được người khác cập nhật. Tải lại rồi thử lại nhé.";
    public const string ClosedForAddMessage = "Đơn đã đóng, không thêm món được.";
    public const string ClosedMessage = "Đơn đã đóng, không sửa được nữa.";
    public const string ItemAlreadyCookedMessage = "Món này bếp đã làm, cần bếp xác nhận mới huỷ được.";
    public const string OnlyOpenCanCancelMessage = "Chỉ huỷ được đơn chưa thanh toán.";
    public const string StatusSequenceMessage = "Món phải chuyển lần lượt Chờ → Đang làm → Xong.";

    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    /// <summary>Số đơn hiển thị cho khách/bếp — DB cấp từ sequence, không trùng.</summary>
    public int Code { get; private set; }

    public Guid ShiftId { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Open;

    /// <summary>Tổng tiền các món chưa huỷ.</summary>
    public decimal Total { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Phiên bản dòng (xmin) — client gửi lại qua <c>If-Match</c>.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public static Order Create(Guid shiftId, IEnumerable<(MenuItem Item, int Qty)> lines, DateTimeOffset now)
    {
        var order = new Order { ShiftId = shiftId, CreatedAt = now };
        foreach (var (item, qty) in lines)
        {
            order.AddLine(item, qty);
        }

        order.Touch(now);
        return order;
    }

    public void AddItem(MenuItem item, int qty, DateTimeOffset now)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException(ClosedForAddMessage);
        }

        AddLine(item, qty);
        Touch(now);
    }

    /// <summary>Thu ngân huỷ món — chỉ khi bếp chưa bắt tay làm.</summary>
    public void CancelItem(Guid itemId, DateTimeOffset now)
    {
        var item = OpenItem(itemId);
        if (item.Status == OrderItemStatus.Cancelled)
        {
            return;
        }

        if (item.Status != OrderItemStatus.Pending)
        {
            throw new DomainException(ItemAlreadyCookedMessage);
        }

        item.Status = OrderItemStatus.Cancelled;
        Touch(now);
    }

    /// <summary>Bếp đổi trạng thái món, đúng thứ tự Chờ → Đang làm → Xong, không nhảy cóc, không lùi.</summary>
    public void SetItemStatus(Guid itemId, OrderItemStatus target, DateTimeOffset now)
    {
        var item = OpenItem(itemId);
        if ((item.Status, target) is not ((OrderItemStatus.Pending, OrderItemStatus.Preparing) or (OrderItemStatus.Preparing, OrderItemStatus.Done)))
        {
            throw new DomainException(StatusSequenceMessage);
        }

        item.Status = target;
        Touch(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException(OnlyOpenCanCancelMessage);
        }

        Status = OrderStatus.Cancelled;
        Touch(now);
        Raise(new OrderCancelled(Id, ShiftId, now));
    }

    private void AddLine(MenuItem item, int qty)
    {
        if (!item.IsAvailable)
        {
            throw new DomainException($"Món {item.Name} vừa hết hàng, vui lòng bỏ khỏi đơn.");
        }

        _items.Add(new OrderItem(item, qty, _items.Count + 1));
    }

    private OrderItem OpenItem(Guid itemId)
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException(ClosedMessage);
        }

        return _items.SingleOrDefault(i => i.Id == itemId) ?? throw new NotFoundException("Không tìm thấy món trong đơn.");
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Total = _items.Where(i => i.Status != OrderItemStatus.Cancelled).Sum(i => i.UnitPrice * i.Qty);
    }
}

/// <summary>Một dòng món. Tên và giá CHỤP LẠI lúc gọi — đổi giá thực đơn sau đó không làm đổi đơn cũ.</summary>
public sealed class OrderItem
{
    private OrderItem()
    {
    }

    internal OrderItem(MenuItem item, int qty, int line)
    {
        MenuItemId = item.Id;
        Name = item.Name;
        UnitPrice = item.Price;
        Qty = qty;
        Line = line;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid MenuItemId { get; private set; }

    public string Name { get; private set; } = "";

    public decimal UnitPrice { get; private set; }

    public int Qty { get; private set; }

    /// <summary>Thứ tự gọi món trong đơn, để hiển thị ổn định.</summary>
    public int Line { get; private set; }

    public OrderItemStatus Status { get; internal set; } = OrderItemStatus.Pending;
}

[Topic(Topics.OrderCancelled)]
public sealed record OrderCancelled(Guid OrderId, Guid ShiftId, DateTimeOffset CancelledAt) : IntegrationEvent
{
    public override string PartitionKey => OrderId.ToString();
}
