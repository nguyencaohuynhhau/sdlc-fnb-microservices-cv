using Cashier.Api.Contracts;
using Cashier.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel;

namespace Cashier.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(PayOrderHandler handler, ILogger<PaymentsController> log) : ControllerBase
{
    /// <summary>
    /// Thu tiền một đơn. <c>Idempotency-Key</c> (UUID, POS sinh một lần cho mỗi lần mở form thu) là bắt
    /// buộc: gửi lại cùng key trả nguyên văn phản hồi lần đầu, không ghi bút toán thứ hai.
    /// </summary>
    [Authorize(Roles = "Cashier,Owner")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreatePaymentRequest req, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!Guid.TryParse(idempotencyKey, out var key))
        {
            // Chỉ log lý do, không log giá trị key.
            log.LogWarning("Từ chối thu tiền: Idempotency-Key {Reason}", idempotencyKey is null ? "thiếu" : "không phải UUID");
            throw new InvalidRequestException("Yêu cầu không hợp lệ.");
        }

        var body = await handler.HandleAsync(key, new PayOrder(req.OrderId, req.ExpectedTotal, req.Method), ct);
        return Content(body, "application/json");
    }
}
