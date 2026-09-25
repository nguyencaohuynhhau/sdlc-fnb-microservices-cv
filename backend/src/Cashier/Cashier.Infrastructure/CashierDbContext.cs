using Cashier.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;

namespace Cashier.Infrastructure;

public sealed class CashierDbContext(DbContextOptions<CashierDbContext> options) : DbContext(options)
{
    public DbSet<Shift> Shifts => Set<Shift>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Shift>(e =>
        {
            e.ToTable("shifts");
            e.Ignore(x => x.Events);
            e.Property(x => x.OpenedBy).HasMaxLength(50);
            e.Property(x => x.OpeningFloat).HasPrecision(14, 2);
            e.Property(x => x.CountedCash).HasPrecision(14, 2);
            e.Property(x => x.ExpectedCash).HasPrecision(14, 2);
            e.Property(x => x.Variance).HasPrecision(14, 2);
            e.Property(x => x.Version).IsRowVersion();

            // "Chỉ một ca mở" giữ ở DB. Postgres mặc định coi các NULL là KHÁC nhau trong unique index,
            // nên index trên closed_at IS NULL chỉ chặn được trùng khi thêm NULLS NOT DISTINCT.
            e.HasIndex(x => x.ClosedAt)
                .IsUnique()
                .HasFilter("closed_at IS NULL")
                .AreNullsDistinct(false)
                .HasDatabaseName("ux_shifts_single_open");
        });

        b.AddMessaging().UseSnakeCaseNames();
    }
}
