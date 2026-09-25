using Cashier.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;

namespace Cashier.Infrastructure;

public sealed class CashierDbContext(DbContextOptions<CashierDbContext> options) : DbContext(options)
{
    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Shift>(e =>
        {
            e.ToTable("shifts");
            e.Ignore(x => x.Events);
            e.Property(x => x.OpenedBy).HasMaxLength(50);
            e.Property(x => x.OpeningFloat).HasPrecision(18, 2);
            e.Property(x => x.CountedCash).HasPrecision(18, 2);
            e.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            e.Property(x => x.Variance).HasPrecision(18, 2);
            e.Property(x => x.Version).IsRowVersion();

            // "Chỉ một ca mở" giữ ở DB. Postgres mặc định coi các NULL là KHÁC nhau trong unique index,
            // nên index trên closed_at IS NULL chỉ chặn được trùng khi thêm NULLS NOT DISTINCT.
            e.HasIndex(x => x.ClosedAt)
                .IsUnique()
                .HasFilter("closed_at IS NULL")
                .AreNullsDistinct(false)
                .HasDatabaseName("ux_shifts_single_open");
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.ShiftId);

            // Chốt cuối ở DB: một đơn chỉ có một bút toán hoàn tất, dù hai máy POS dùng hai key khác nhau.
            e.HasIndex(x => x.OrderId).IsUnique().HasFilter("status = 'Completed'").HasDatabaseName("ux_payments_order_completed");
        });

        b.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_keys");
            e.HasKey(x => new { x.Key, x.Endpoint });
            e.Property(x => x.Endpoint).HasMaxLength(100);
            e.Property(x => x.RequestHash).HasMaxLength(64);
        });

        b.AddMessaging().UseSnakeCaseNames();
    }
}

/// <summary>
/// Một lần gửi lệnh ghi có <c>Idempotency-Key</c>. Chỉ phản hồi thành công được lưu — lỗi rollback
/// cả dòng này để bấm lại cùng key vẫn chạy lại được.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Key { get; set; }

    public string Endpoint { get; set; } = "";

    /// <summary>SHA-256 hex của body đã chuẩn hoá — cùng key mà body khác là lỗi phía client (422).</summary>
    public string RequestHash { get; set; } = "";

    public int? ResponseStatus { get; set; }

    /// <summary>Nguyên văn JSON đã trả lần đầu — gửi lại trả đúng từng byte.</summary>
    public string? ResponseBody { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
