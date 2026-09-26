using Cashier.Api.Contracts;
using Cashier.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Web;

namespace Cashier.Api.Controllers;

[ApiController]
[Route("api/shifts")]
public sealed class ShiftsController : ControllerBase
{
    /// <summary>Mọi vai trò (kể cả bếp) cần biết đang có ca hay không; 204 khi chưa mở ca.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current([FromServices] IShiftRepository shifts, CancellationToken ct) =>
        await shifts.CurrentAsync(ct) is { } s ? Ok(ShiftResponse.From(s)) : NoContent();

    [Authorize(Roles = "Cashier,Owner")]
    [HttpPost("open")]
    public async Task<IActionResult> Open(OpenShiftRequest req, [FromServices] OpenShiftHandler handler, CancellationToken ct)
    {
        var openedBy = User.FindFirst(JwtSettings.NameClaim)?.Value ?? "?";
        var shift = await handler.HandleAsync(openedBy, req.OpeningFloat, ct);
        return StatusCode(StatusCodes.Status201Created, ShiftResponse.From(shift));
    }

    [Authorize(Roles = "Cashier,Owner")]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CloseShiftRequest req, [FromServices] CloseShiftHandler handler, CancellationToken ct) =>
        Ok(ShiftSummaryResponse.From(await handler.HandleAsync(id, req.CountedCash!.Value, ct)));
}
