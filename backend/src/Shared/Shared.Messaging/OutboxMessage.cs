namespace Shared.Messaging;

/// <summary>
/// Một sự kiện chờ đẩy lên Kafka. Ghi cùng transaction với thay đổi nghiệp vụ nên
/// không bao giờ có chuyện "DB đã đổi mà sự kiện mất" hay ngược lại.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public required string Topic { get; init; }
    public required string Key { get; init; }
    public required string Type { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>Dấu vết sự kiện đã xử lý — khoá chính <see cref="MessageId"/> chặn xử lý hai lần.</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; init; }
    public DateTimeOffset HandledAt { get; init; }
}
