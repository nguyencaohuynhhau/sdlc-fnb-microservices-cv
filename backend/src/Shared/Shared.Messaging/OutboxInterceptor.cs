using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.Kernel;

namespace Shared.Messaging;

/// <summary>
/// Ngay trước SaveChanges: gom sự kiện từ mọi aggregate đang được theo dõi, ghi thành
/// <see cref="OutboxMessage"/> trong CÙNG transaction. Code nghiệp vụ chỉ cần <c>Raise(...)</c>.
/// </summary>
public sealed class OutboxInterceptor(OutboxSignal signal) : SaveChangesInterceptor
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private bool _hasNewMessages;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is { } db)
        {
            var entities = db.ChangeTracker.Entries<Entity>().Select(e => e.Entity).Where(e => e.Events.Count > 0).ToList();
            foreach (var entity in entities)
            {
                foreach (var e in entity.Events)
                {
                    db.Set<OutboxMessage>().Add(ToOutbox(e));
                    _hasNewMessages = true;
                }

                entity.ClearEvents();
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (_hasNewMessages)
        {
            // Đánh thức publisher ngay thay vì chờ chu kỳ 500ms kế tiếp.
            _hasNewMessages = false;
            signal.Notify();
        }

        return base.SavedChangesAsync(eventData, result, ct);
    }

    public static OutboxMessage ToOutbox(IntegrationEvent e)
    {
        var type = e.GetType();
        var topic = type.GetCustomAttribute<TopicAttribute>()?.Name
            ?? throw new InvalidOperationException($"{type.Name} thiếu [Topic].");
        return new OutboxMessage
        {
            Id = e.MessageId,
            Topic = topic,
            Key = e.PartitionKey,
            Type = type.Name,
            Payload = JsonSerializer.Serialize(e, type, Json),
            OccurredAt = e.OccurredAt,
        };
    }
}

/// <summary>Chuông báo "có outbox mới" giữa interceptor (scoped) và publisher (singleton).</summary>
public sealed class OutboxSignal
{
    private readonly SemaphoreSlim _gate = new(0, 1);

    public void Notify()
    {
        if (_gate.CurrentCount == 0)
        {
            try
            {
                _gate.Release();
            }
            catch (SemaphoreFullException)
            {
                // Đã có người rung chuông — bỏ qua.
            }
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _gate.WaitAsync(timeout, ct);
}
