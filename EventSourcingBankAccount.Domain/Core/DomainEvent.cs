namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// 领域事件基类
/// </summary>
public abstract class DomainEvent
{
    public Guid EventId { get; protected set; } = Guid.NewGuid();
    public DateTime Timestamp { get; protected set; } = DateTime.UtcNow;
    public int Version { get; set; }
    public string AggregateId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    
    protected DomainEvent()
    {
        EventType = GetType().Name;
    }
}