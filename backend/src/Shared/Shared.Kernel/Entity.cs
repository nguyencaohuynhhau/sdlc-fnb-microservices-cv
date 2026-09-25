namespace Shared.Kernel;

/// <summary>
/// Gốc aggregate. Sự kiện tích hợp được gom ở đây rồi interceptor của Shared.Messaging
/// chép sang outbox trong CÙNG transaction với thay đổi nghiệp vụ.
/// </summary>
public abstract class Entity
{
    private readonly List<IntegrationEvent> _events = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public IReadOnlyList<IntegrationEvent> Events => _events;

    protected void Raise(IntegrationEvent e) => _events.Add(e);

    public void ClearEvents() => _events.Clear();
}
