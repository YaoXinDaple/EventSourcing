namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// 聚合根基类
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _uncommittedEvents = new();
    
    public string Id { get; protected set; } = string.Empty;
    public int Version { get; protected set; }
    
    public IReadOnlyList<DomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    
    protected void AddEvent(DomainEvent @event)
    {
        @event.AggregateId = Id;
        @event.Version = Version + _uncommittedEvents.Count + 1;
        _uncommittedEvents.Add(@event);
    }
    
    public void MarkEventsAsCommitted()
    {
        Version += _uncommittedEvents.Count;
        _uncommittedEvents.Clear();
    }
    
    public void LoadFromHistory(IEnumerable<DomainEvent> events)
    {
        foreach (var @event in events.OrderBy(e => e.Version))
        {
            ApplyEvent(@event);
            Version = @event.Version;
        }
    }
    
    protected abstract void ApplyEvent(DomainEvent @event);
}