using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Shared.Messaging;

public static class MessagingDbConfig
{
    /// <summary>Map <c>outbox_messages</c>/<c>inbox_messages</c> — mọi DbContext của dịch vụ gọi hàm này.</summary>
    public static ModelBuilder AddMessaging(this ModelBuilder b)
    {
        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            // Publisher chỉ quét dòng chưa đẩy — index một phần giữ nó nhỏ.
            e.HasIndex(x => x.OccurredAt).HasFilter("published_at IS NULL");
        });
        b.Entity<InboxMessage>().HasKey(x => x.MessageId);
        return b;
    }

    /// <summary>
    /// Đặt tên bảng/cột snake_case theo quy ước PostgreSQL. Gọi CUỐI <c>OnModelCreating</c>.
    /// </summary>
    public static ModelBuilder UseSnakeCaseNames(this ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } table && entity.BaseType is null)
            {
                entity.SetTableName(ToSnake(table));
            }

            foreach (var p in entity.GetProperties())
            {
                // Row version kiểu uint phải map vào cột hệ thống xmin; đổi tên sẽ tạo cột thật
                // không bao giờ tự đổi và concurrency token mất tác dụng trong im lặng.
                p.SetColumnName(p.IsConcurrencyToken && p.ClrType == typeof(uint) ? "xmin" : ToSnake(p.Name));
            }

            foreach (var k in entity.GetKeys())
            {
                k.SetName(ToSnake(k.GetName()!));
            }

            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnake(fk.GetConstraintName()!));
            }

            foreach (var ix in entity.GetIndexes())
            {
                ix.SetDatabaseName(ToSnake(ix.GetDatabaseName()!));
            }
        }

        return b;
    }

    public static string ToSnake(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_' && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
