using Cashier.Domain;
using Cashier.Infrastructure;
using Identity.Domain;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain;
using Ordering.Infrastructure;

namespace Seeder;

/// <summary>
/// Dữ liệu demo cho ba database. Chạy bao nhiêu lần cũng được: mỗi phần chỉ thêm cái còn thiếu.
/// Ghi thẳng vào database (không qua outbox) nên tự chép ca của cashier sang <c>known_shifts</c> của ordering.
/// </summary>
public static class DemoData
{
    public const string AllowedEnvironment = "Development";

    private static readonly (string Username, Role Role)[] Users =
        [("owner", Role.Owner), ("cashier", Role.Cashier), ("kitchen", Role.Kitchen)];

    private static readonly (string Name, decimal Price)[] Menu =
    [
        ("Cà phê đen đá", 25_000m), ("Cà phê sữa đá", 29_000m), ("Bạc xỉu", 32_000m), ("Cà phê muối", 35_000m),
        ("Espresso", 35_000m), ("Americano", 39_000m), ("Cappuccino", 49_000m), ("Latte", 49_000m),
        ("Caramel macchiato", 55_000m), ("Cold brew", 45_000m), ("Trà đào cam sả", 45_000m), ("Trà vải", 42_000m),
        ("Trà sen vàng", 45_000m), ("Matcha latte", 52_000m), ("Trà sữa trân châu", 39_000m), ("Bánh mì thịt", 30_000m),
        ("Croissant bơ", 35_000m), ("Bánh chuối", 25_000m), ("Tiramisu", 45_000m), ("Bánh flan", 20_000m),
    ];

    /// <summary>Món hết hàng sẵn để demo luật "món vừa hết hàng" trên POS.</summary>
    private const string SoldOut = "Bánh flan";

    public static bool IsAllowed(string? environment) => environment == AllowedEnvironment;

    /// <param name="connectionString">Tên database → connection string.</param>
    public static async Task<string> SeedAsync(Func<string, string> connectionString, string password, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var identity = new IdentityDbContext(Options<IdentityDbContext>(connectionString("fnb_identity")));
        await using var ordering = new OrderingDbContext(Options<OrderingDbContext>(connectionString("fnb_ordering")));
        await using var cashier = new CashierDbContext(Options<CashierDbContext>(connectionString("fnb_cashier")));
        await identity.Database.MigrateAsync(ct);
        await ordering.Database.MigrateAsync(ct);
        await cashier.Database.MigrateAsync(ct);

        await SeedUsersAsync(identity, password, ct);
        await SeedMenuAsync(ordering, ct);
        await SeedShiftsAsync(cashier, now, ct);
        await MirrorShiftsAsync(cashier, ordering, ct);
        await SeedOrdersAsync(ordering, now, ct);

        var names = Users.Select(u => u.Username).ToArray();
        var users = await identity.Users.CountAsync(u => names.Contains(u.Username), ct);
        var menu = await ordering.MenuItems.CountAsync(ct);
        var closed = await cashier.Shifts.CountAsync(s => s.ClosedAt != null, ct);
        var open = await cashier.Shifts.CountAsync(s => s.ClosedAt == null, ct);
        var orders = await ordering.Orders.CountAsync(o => o.Status == OrderStatus.Open, ct);
        return $"Seeded: {users} users, {menu} menu items, {closed} closed shift, {open} open shift, {orders} open orders";
    }

    private static DbContextOptions<T> Options<T>(string cs)
        where T : DbContext => new DbContextOptionsBuilder<T>().UseNpgsql(cs).Options;

    /// <summary>Tài khoản đã có thì giữ nguyên, kể cả mật khẩu — không ghi đè thứ người dùng đã đổi.</summary>
    private static async Task SeedUsersAsync(IdentityDbContext db, string password, CancellationToken ct)
    {
        var hasher = new PasswordHasher<User>();
        foreach (var (username, role) in Users)
        {
            if (await db.Users.AnyAsync(u => u.Username == username, ct))
            {
                continue;
            }

            var user = new User(username, role);
            user.SetPasswordHash(hasher.HashPassword(user, password));
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedMenuAsync(OrderingDbContext db, CancellationToken ct)
    {
        var existing = await db.MenuItems.Select(m => m.Name).ToListAsync(ct);
        foreach (var (name, price) in Menu.Where(m => !existing.Contains(m.Name)))
        {
            var item = new MenuItem(name, price);
            item.SetAvailable(name != SoldOut);
            db.MenuItems.Add(item);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Chỉ khi cashier chưa có ca nào: một ca hôm qua đã đóng, một ca mở từ 2 tiếng trước.</summary>
    private static async Task SeedShiftsAsync(CashierDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Shifts.AnyAsync(ct))
        {
            return;
        }

        var yesterday = Shift.Open(null, "cashier", 500_000m, now.AddDays(-1).AddHours(-10));
        yesterday.Close(now.AddDays(-1));
        db.Shifts.Add(yesterday);
        db.Shifts.Add(Shift.Open(yesterday, "cashier", 500_000m, now.AddHours(-2)));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Thay cho ShiftOpened/ShiftClosed không được phát (seed không đi qua outbox).</summary>
    private static async Task MirrorShiftsAsync(CashierDbContext cashier, OrderingDbContext ordering, CancellationToken ct)
    {
        foreach (var s in await cashier.Shifts.AsNoTracking().ToListAsync(ct))
        {
            var known = await ordering.KnownShifts.FindAsync([s.Id], ct);
            if (known is null)
            {
                known = new KnownShift { ShiftId = s.Id };
                ordering.KnownShifts.Add(known);
            }

            known.OpenedAt = s.OpenedAt;
            known.ClosedAt = s.ClosedAt;
        }

        await ordering.SaveChangesAsync(ct);
    }

    /// <summary>Ba đơn đang mở cho ca hiện hành, chỉ khi ca đó chưa có đơn nào. Đơn đầu có một món bếp đang làm.</summary>
    private static async Task SeedOrdersAsync(OrderingDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var shift = await db.KnownShifts.AsNoTracking()
            .Where(s => s.OpenedAt != null && s.ClosedAt == null)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(ct);
        if (shift is null || await db.Orders.AnyAsync(o => o.ShiftId == shift.ShiftId, ct))
        {
            return;
        }

        var menu = await db.MenuItems.ToDictionaryAsync(m => m.Name, ct);
        (string Name, int Qty)[][] orders =
        [
            [("Cà phê sữa đá", 2), ("Bánh mì thịt", 1)],
            [("Trà đào cam sả", 1)],
            [("Bạc xỉu", 1), ("Croissant bơ", 2)],
        ];
        for (var i = 0; i < orders.Length; i++)
        {
            var order = Order.Create(shift.ShiftId, orders[i].Select(l => (menu[l.Name], l.Qty)), now.AddMinutes(-30 + (i * 10)));
            if (i == 0)
            {
                order.SetItemStatus(order.Items.First().Id, OrderItemStatus.Preparing, now);
            }

            db.Orders.Add(order);
        }

        await db.SaveChangesAsync(ct);
    }
}
