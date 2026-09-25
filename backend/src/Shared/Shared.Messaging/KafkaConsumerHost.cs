using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kernel;

namespace Shared.Messaging;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    /// <summary>Chỉ sửa DbContext; host lưu cùng dòng inbox trong một transaction. Không tự SaveChanges.</summary>
    Task HandleAsync(TEvent e, CancellationToken ct);
}

/// <summary>
/// Nhận một topic. Mỗi message: mở transaction → bỏ qua nếu <c>message_id</c> đã có trong inbox →
/// gọi handler → ghi inbox → COMMIT → rồi mới commit offset Kafka.
/// At-least-once của Kafka cộng inbox cho ra hiệu ứng exactly-once.
/// </summary>
public sealed class KafkaConsumerHost<TDb, TEvent>(
    IServiceScopeFactory scopes,
    KafkaOptions options,
    ILogger<KafkaConsumerHost<TDb, TEvent>> log) : BackgroundService
    where TDb : DbContext
    where TEvent : IntegrationEvent
{
    private readonly string _topic = TopicOf();

    protected override Task ExecuteAsync(CancellationToken ct) =>
        Task.Factory.StartNew(() => RunAsync(ct), ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken ct)
    {
        await EnsureTopicAsync(ct);
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.ServiceName,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(_topic);

        var backoff = TimeSpan.Zero;
        while (!ct.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(ct);
                var e = JsonSerializer.Deserialize<TEvent>(result.Message.Value, OutboxInterceptor.Json)
                    ?? throw new InvalidOperationException("Message rỗng.");
                await ProcessAsync(scopes, e, ct);
                consumer.Commit(result);
                backoff = TimeSpan.Zero;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Không commit offset: quay lại đúng message này và thử lại, đợi lâu dần.
                backoff = TimeSpan.FromMilliseconds(Math.Min(30_000, Math.Max(500, backoff.TotalMilliseconds * 2)));
                log.LogError(ex, "Xử lý {Topic} lỗi, thử lại sau {Backoff}", _topic, backoff);
                if (result is not null)
                {
                    consumer.Seek(result.TopicPartitionOffset);
                }

                await Task.Delay(backoff, ct);
            }
        }

        consumer.Close();
    }

    /// <summary>Xử lý một sự kiện với inbox khử trùng. Trả <c>false</c> nếu đã xử lý trước đó.</summary>
    public static async Task<bool> ProcessAsync(IServiceScopeFactory scopes, TEvent e, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDb>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Set<InboxMessage>().AnyAsync(m => m.MessageId == e.MessageId, ct))
        {
            return false;
        }

        await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<TEvent>>().HandleAsync(e, ct);
        db.Set<InboxMessage>().Add(new InboxMessage { MessageId = e.MessageId, HandledAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// Tạo topic nếu chưa có. Consumer không tự tạo topic, và nếu đăng ký topic chưa tồn tại
    /// thì phải chờ lần làm mới metadata (mặc định 5 phút) mới thấy — quá chậm cho "mở ca rồi bán".
    /// </summary>
    private async Task EnsureTopicAsync(CancellationToken ct)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = options.BootstrapServers }).Build();
        for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await admin.CreateTopicsAsync([new TopicSpecification { Name = _topic, NumPartitions = 3, ReplicationFactor = 1 }]);
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Kafka chưa sẵn sàng (lần {Attempt}), thử lại", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, attempt)), ct);
            }
        }
    }

    private static string TopicOf() =>
        typeof(TEvent).GetCustomAttributes(typeof(TopicAttribute), false).Cast<TopicAttribute>().SingleOrDefault()?.Name
        ?? throw new InvalidOperationException($"{typeof(TEvent).Name} thiếu [Topic].");
}
