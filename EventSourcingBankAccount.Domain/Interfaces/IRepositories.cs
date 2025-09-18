using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Interfaces;

/// <summary>
/// �¼��洢�ӿ�
/// </summary>
public interface IEventStore
{
    Task SaveEventsAsync(string aggregateId, IEnumerable<DomainEvent> events, int expectedVersion);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, int fromVersion);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, DateTime pointInTime);
    Task<IEnumerable<DomainEvent>> GetAllEventsAsync();
}

/// <summary>
/// ���մ洢�ӿ�
/// </summary>
public interface ISnapshotStore
{
    Task SaveSnapshotAsync(ISnapshot snapshot);
    Task<T?> GetSnapshotAsync<T>(string aggregateId) where T : class, ISnapshot;
    Task<T?> GetSnapshotAsync<T>(string aggregateId, DateTime pointInTime) where T : class, ISnapshot;
}

/// <summary>
/// �ۺϴ洢�ӿ�
/// </summary>
public interface IAggregateStore
{
    Task<T?> GetByIdAsync<T>(string id) where T : AggregateRoot;
    Task<T?> GetByIdAsync<T>(string id, DateTime pointInTime) where T : AggregateRoot;
    Task SaveAsync<T>(T aggregate) where T : AggregateRoot;
}