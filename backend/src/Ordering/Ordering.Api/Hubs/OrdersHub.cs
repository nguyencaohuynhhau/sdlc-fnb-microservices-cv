using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Ordering.Application;

namespace Ordering.Api.Hubs;

/// <summary>POS và bếp nghe thay đổi đơn của cùng một ca. Client gọi <c>JoinShift(shiftId)</c> sau khi kết nối.</summary>
[Authorize]
public sealed class OrdersHub : Hub
{
    public Task JoinShift(Guid shiftId) => Groups.AddToGroupAsync(Context.ConnectionId, Group(shiftId));

    public static string Group(Guid shiftId) => $"shift:{shiftId}";
}

public sealed class SignalROrderNotifier(IHubContext<OrdersHub> hub) : IOrderNotifier
{
    public Task NotifyAsync(string eventName, OrderView order, CancellationToken ct) =>
        hub.Clients.Group(OrdersHub.Group(order.ShiftId)).SendAsync(eventName, order, ct);
}
