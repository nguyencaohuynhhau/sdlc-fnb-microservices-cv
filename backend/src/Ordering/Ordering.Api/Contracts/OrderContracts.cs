using System.ComponentModel.DataAnnotations;
using Ordering.Domain;

namespace Ordering.Api.Contracts;

public sealed record OrderLineRequest([Required] Guid MenuItemId, [Range(1, 99)] int Qty);

public sealed record CreateOrderRequest([Required, MinLength(1), MaxLength(100)] List<OrderLineRequest> Items);

public sealed record SetItemStatusRequest([Required] OrderItemStatus Status);
