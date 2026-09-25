using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Messaging;

/// <summary>
/// Đẩy outbox lên Kafka. Quét NGAY khi khởi động (đẩy nốt những gì còn sót lúc dịch vụ sập),
/// sau đó mỗi 500ms hoặc ngay khi interceptor rung chuông.
/// </summary>
/// <remarks>ponytail: giả định một bản chạy mỗi dịch vụ; chạy nhiều bản thì cần <c>FOR UPDATE SKIP LOCKED</c>.</remarks>
public sealed class OutboxPublisherHost<TDb>(
    IServiceScopeFactory scopes,
    IProducer<string, string> producer,
    OutboxSignal signal,
    TimeProvider clock,
    ILogger<OutboxPublisherHost<TDb>> log) : BackgroundService
    where TDb : DbContext
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.Zero;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(ct);
                backoff = TimeSpan.Zero;
                await signal.WaitAsync(Interval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Kafka/DB chưa sẵn sàng: đợi lâu dần, không làm sập dịch vụ.
                backoff = TimeSpan.FromMilliseconds(Math.Min(30_000, Math.Max(500, backoff.TotalMilliseconds * 2)));
                log.LogWarning(ex, "Outbox publish lỗi, thử lại sau {Backoff}", backoff);
                await Task.Delay(backoff, ct);
            }
        }
    }

    /// <summary>Đẩy mọi dòng <c>published_at IS NULL</c>, theo thứ tự xảy ra. Trả số message đã đẩy.</summary>
    public async Task<int> PublishPendingAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDb>();
        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var m in pending)
        {
            await producer.ProduceAsync(m.Topic, new Message<string, string>
            {
                Key = m.Key,
                Value = m.Payload,
                Headers = new Headers { { "message-type", System.Text.Encoding.UTF8.GetBytes(m.Type) } },
            }, ct);
            m.PublishedAt = clock.GetUtcNow();

            // Lưu từng dòng: sập giữa chừng thì chỉ đẩy lại đúng message chưa đánh dấu (at-least-once).
            await db.SaveChangesAsync(ct);
        }

        return pending.Count;
    }
}
