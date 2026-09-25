using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Ordering.Application;
using Ordering.Domain;

namespace Ordering.Infrastructure;

public sealed class MenuCatalog(OrderingDbContext db, IDistributedCache cache, ILogger<MenuCatalog> log) : IMenuCatalog
{
    public const string CacheKey = "menu:v1";

    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<MenuItemView>> GetMenuAsync(CancellationToken ct)
    {
        try
        {
            if (await cache.GetStringAsync(CacheKey, ct) is { } cached)
            {
                return JsonSerializer.Deserialize<List<MenuItemView>>(cached)!;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Redis chỉ để tăng tốc: nó chết thì vẫn bán được, đọc thẳng DB.
            log.LogWarning(ex, "Đọc cache thực đơn lỗi, đọc DB");
        }

        var menu = await db.MenuItems.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new MenuItemView(m.Id, m.Name, m.Price, m.IsAvailable))
            .ToListAsync(ct);

        try
        {
            await cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(menu), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Ghi cache thực đơn lỗi");
        }

        return menu;
    }

    public async Task<IReadOnlyList<MenuItem>> FindActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.MenuItems.AsNoTracking().Where(m => m.IsActive && ids.Contains(m.Id)).ToListAsync(ct);
}
