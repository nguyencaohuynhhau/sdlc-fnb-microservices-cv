using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordering.Application;

namespace Ordering.Api.Controllers;

[ApiController]
public sealed class MenuController : ControllerBase
{
    [Authorize(Roles = "Cashier,Owner")]
    [HttpGet("api/menu")]
    public Task<IReadOnlyList<MenuItemView>> Get([FromServices] IMenuCatalog menu, CancellationToken ct) => menu.GetMenuAsync(ct);
}
