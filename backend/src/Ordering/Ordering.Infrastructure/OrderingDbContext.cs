using Microsoft.EntityFrameworkCore;
using Ordering.Domain;
using Shared.Messaging;

namespace Ordering.Infrastructure;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<KnownShift> KnownShifts => Set<KnownShift>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasSequence<int>("order_code_seq");

        b.Entity<MenuItem>(e =>
        {
            e.ToTable("menu_items");
            e.Ignore(x => x.Events);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasIndex(x => x.IsActive);
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.Ignore(x => x.Events);
            e.Property(x => x.Code).HasDefaultValueSql("nextval('order_code_seq')");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ShiftId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Version).IsRowVersion();
            e.HasMany(x => x.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");

            // Id do domain sinh. Không có dòng này, EF coi món mới thêm vào đơn đã có là "đã tồn tại"
            // (khoá đã có giá trị) và UPDATE thay vì INSERT.
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<KnownShift>(e =>
        {
            e.ToTable("known_shifts");
            e.HasKey(x => x.ShiftId);
            e.Property(x => x.ShiftId).ValueGeneratedNever();
        });

        b.AddMessaging().UseSnakeCaseNames();
    }
}
