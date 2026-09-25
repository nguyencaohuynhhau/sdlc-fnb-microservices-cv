using System.Text.Json.Serialization;

namespace Shared.Kernel;

/// <summary>
/// Sự kiện đi qua Kafka. <see cref="MessageId"/> để bên nhận khử trùng qua inbox;
/// <see cref="PartitionKey"/> giữ thứ tự theo đơn/ca trong cùng partition.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public abstract string PartitionKey { get; }
}

/// <summary>Tên topic Kafka của một sự kiện, ví dụ <c>fnb.cashier.shift-opened.v1</c>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TopicAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
