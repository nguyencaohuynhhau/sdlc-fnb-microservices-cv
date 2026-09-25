using Ordering.Domain;
using Shared.Kernel;

namespace Ordering.Application;

/// <summary>Các lệnh ghi lên đơn. Lưu xong mới báo SignalR — màn hình không bao giờ thấy thứ chưa commit.</summary>
public sealed class OrderService(
    IOrderRepository orders,
    ICurrentShift currentShift,
    IMenuCatalog menu,
    IOrderNotifier notifier,
    TimeProvider clock)
{
    public async Task<OrderView> CreateAsync(IReadOnlyList<(Guid MenuItemId, int Qty)> lines, CancellationToken ct)
    {
        var shift = await currentShift.GetAsync(ct) ?? throw new DomainException(Order.NoOpenShiftMessage);
        var items = await LoadMenuItemsAsync(lines.Select(l => l.MenuItemId), ct);
        var order = Order.Create(shift.ShiftId, lines.Select(l => (items[l.MenuItemId], l.Qty)), clock.GetUtcNow());
        orders.Add(order);
        _ = await orders.TrySaveAsync(ct); // đơn mới: chưa có phiên bản nào để xung đột
        return await NotifyAsync(OrderEvents.Created, order, ct);
    }

    public async Task<OrderView> AddItemAsync(Guid orderId, uint? expectedVersion, Guid menuItemId, int qty, CancellationToken ct)
    {
        var item = (await LoadMenuItemsAsync([menuItemId], ct))[menuItemId];
        return await MutateAsync(orderId, expectedVersion, OrderEvents.Updated, (o, now) => o.AddItem(item, qty, now), ct);
    }

    public Task<OrderView> CancelItemAsync(Guid orderId, uint? expectedVersion, Guid itemId, CancellationToken ct) =>
        MutateAsync(orderId, expectedVersion, OrderEvents.Updated, (o, now) => o.CancelItem(itemId, now), ct);

    public Task<OrderView> SetItemStatusAsync(Guid orderId, uint? expectedVersion, Guid itemId, OrderItemStatus status, CancellationToken ct) =>
        MutateAsync(orderId, expectedVersion, OrderEvents.ItemStatusChanged, (o, now) => o.SetItemStatus(itemId, status, now), ct);

    public Task<OrderView> CancelAsync(Guid orderId, uint? expectedVersion, CancellationToken ct) =>
        MutateAsync(orderId, expectedVersion, OrderEvents.Cancelled, (o, now) => o.Cancel(now), ct);

    /// <summary>
    /// Cashier thu tiền (qua gRPC). Không có If-Match: bếp đổi trạng thái món cũng làm xmin đổi mà tổng
    /// tiền không đổi, nên xung đột ở đây là chuyện thường — đọc lại, KIỂM LẠI tổng, thử lại.
    /// </summary>
    /// <returns>Kết quả và tổng thật của đơn lúc quyết định.</returns>
    public async Task<(MarkPaidOutcome Outcome, decimal ActualTotal)> MarkPaidAsync(
        Guid orderId, decimal expectedTotal, Guid paymentId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var order = await orders.FindAsync(orderId, ct) ?? throw new NotFoundException(Order.NotFoundMessage);
            var outcome = order.MarkPaid(paymentId, expectedTotal, clock.GetUtcNow());
            if (outcome != MarkPaidOutcome.Ok)
            {
                return (outcome, order.Total);
            }

            if (await orders.TrySaveAsync(ct))
            {
                await NotifyAsync(OrderEvents.Updated, order, ct);
                return (outcome, order.Total);
            }

            if (attempt == MarkPaidAttempts)
            {
                throw new DomainException(Order.StaleVersionMessage);
            }

            orders.Reset();
        }
    }

    public const int MarkPaidAttempts = 3;

    /// <summary>
    /// Hai lớp chặn ghi đè: so <c>If-Match</c> với phiên bản vừa đọc (bắt client cầm bản cũ), rồi
    /// <c>UPDATE … WHERE xmin = @v</c> lúc lưu (bắt hai request lọt qua bước so cùng lúc).
    /// </summary>
    private async Task<OrderView> MutateAsync(Guid orderId, uint? expectedVersion, string eventName, Action<Order, DateTimeOffset> change, CancellationToken ct)
    {
        var order = await orders.FindAsync(orderId, ct) ?? throw new NotFoundException(Order.NotFoundMessage);
        if (order.Version != expectedVersion)
        {
            throw new DomainException(Order.StaleVersionMessage);
        }

        change(order, clock.GetUtcNow());
        if (!await orders.TrySaveAsync(ct))
        {
            throw new DomainException(Order.StaleVersionMessage);
        }

        return await NotifyAsync(eventName, order, ct);
    }

    private async Task<Dictionary<Guid, MenuItem>> LoadMenuItemsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        var found = await menu.FindActiveAsync(wanted, ct);
        if (found.Count != wanted.Count)
        {
            throw new InvalidRequestException("Món không có trong thực đơn.");
        }

        return found.ToDictionary(m => m.Id);
    }

    private async Task<OrderView> NotifyAsync(string eventName, Order order, CancellationToken ct)
    {
        var view = OrderView.From(order);
        await notifier.NotifyAsync(eventName, view, ct);
        return view;
    }
}
