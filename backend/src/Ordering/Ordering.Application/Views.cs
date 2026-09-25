using Ordering.Domain;

namespace Ordering.Application;

/// <summary>Hình dạng <c>Order</c> trong docs/api/endpoints.md — dùng chung cho HTTP và SignalR.</summary>
public sealed record OrderView(
    Guid Id,
    int Code,
    Guid ShiftId,
    string Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    uint Version,
    IReadOnlyList<OrderItemView> Items)
{
    public static OrderView From(Order o) => new(
        o.Id,
        o.Code,
        o.ShiftId,
        o.Status.ToString(),
        o.Total,
        o.CreatedAt,
        o.Version,
        [.. o.Items.OrderBy(i => i.Line).Select(i => new OrderItemView(i.Id, i.MenuItemId, i.Name, i.UnitPrice, i.Qty, i.Status.ToString()))]);
}

public sealed record OrderItemView(Guid Id, Guid MenuItemId, string Name, decimal UnitPrice, int Qty, string Status);

public sealed record MenuItemView(Guid Id, string Name, decimal Price, bool IsAvailable);

public sealed record CurrentShiftView(Guid ShiftId, DateTimeOffset OpenedAt);
