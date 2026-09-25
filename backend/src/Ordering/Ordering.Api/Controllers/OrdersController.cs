using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Api.Contracts;
using Ordering.Application;
using Ordering.Domain;
using Shared.Kernel;

namespace Ordering.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService service, IOrderRepository orders, ICurrentShift currentShift) : ControllerBase
{
    private uint? ExpectedVersion => RequireIfMatchAttribute.ExpectedVersion(HttpContext);

    /// <summary>Ca mà ordering đã biết — POS chờ tới khi có mới bật "Gửi bếp". Mọi vai trò.</summary>
    [HttpGet("current-shift")]
    public async Task<IActionResult> CurrentShift(CancellationToken ct) =>
        await currentShift.GetAsync(ct) is { } s ? Ok(s) : NoContent();

    [Authorize(Roles = "Cashier,Owner")]
    [HttpGet]
    public async Task<IEnumerable<OrderView>> List([FromQuery] OrderStatus? status, [FromQuery] bool active, CancellationToken ct) =>
        await currentShift.GetAsync(ct) is { } s
            ? (await orders.ListByShiftAsync(s.ShiftId, status, active, newestFirst: true, ct)).Select(OrderView.From)
            : [];

    /// <summary>
    /// Đơn còn việc cho bếp của ca hiện hành, cũ nhất trước — kể cả đơn khách đã trả tiền mà
    /// món chưa xong (thu tiền trước khi pha là chuyện thường).
    /// </summary>
    [Authorize(Roles = "Kitchen,Owner")]
    [HttpGet("/api/kitchen/orders")]
    public async Task<IEnumerable<OrderView>> Kitchen(CancellationToken ct) =>
        await currentShift.GetAsync(ct) is { } s
            ? (await orders.ListByShiftAsync(s.ShiftId, status: null, activeOnly: true, newestFirst: false, ct)).Select(OrderView.From)
            : [];

    [Authorize(Roles = "Cashier,Owner,Kitchen")]
    [HttpGet("{id:guid}")]
    public async Task<OrderView> Get(Guid id, CancellationToken ct) =>
        OrderView.From(await orders.FindAsync(id, ct) ?? throw new NotFoundException(Order.NotFoundMessage));

    [Authorize(Roles = "Cashier,Owner")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync([.. req.Items.Select(i => (i.MenuItemId, i.Qty))], ct));

    [Authorize(Roles = "Cashier,Owner")]
    [RequireIfMatch]
    [HttpPost("{id:guid}/items")]
    public Task<OrderView> AddItem(Guid id, OrderLineRequest req, CancellationToken ct) =>
        service.AddItemAsync(id, ExpectedVersion, req.MenuItemId, req.Qty, ct);

    [Authorize(Roles = "Cashier,Owner")]
    [RequireIfMatch]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public Task<OrderView> CancelItem(Guid id, Guid itemId, CancellationToken ct) =>
        service.CancelItemAsync(id, ExpectedVersion, itemId, ct);

    [Authorize(Roles = "Cashier,Owner")]
    [RequireIfMatch]
    [HttpPost("{id:guid}/cancel")]
    public Task<OrderView> Cancel(Guid id, CancellationToken ct) =>
        service.CancelAsync(id, ExpectedVersion, ct);

    [Authorize(Roles = "Kitchen,Owner")]
    [RequireIfMatch]
    [HttpPatch("{id:guid}/items/{itemId:guid}/status")]
    public Task<OrderView> SetItemStatus(Guid id, Guid itemId, SetItemStatusRequest req, CancellationToken ct) =>
        service.SetItemStatusAsync(id, ExpectedVersion, itemId, req.Status, ct);
}
