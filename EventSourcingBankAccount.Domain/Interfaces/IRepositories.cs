using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Interfaces;

/// <summary>
/// 事件存储接口
/// </summary>
public interface IEventStore
{
    Task SaveEventsAsync(string aggregateId, IEnumerable<DomainEvent> events, int expectedVersion);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, int fromVersion);
    /// <summary>
    /// 获取指定 UTC 时间点之前的事件。
    /// </summary>
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, DateTime pointInTime);
    Task<int> GetLatestVersionAsync(string aggregateId);
    /// <summary>
    /// 获取指定 UTC 时间点之前的最新版本。
    /// </summary>
    Task<int> GetLatestVersionAsync(string aggregateId, DateTime pointInTime);
    Task<bool> ExistsAsync(string aggregateId);
    /// <summary>
    /// 判断聚合在指定 UTC 时间点是否已存在。
    /// </summary>
    Task<bool> ExistsAsync(string aggregateId, DateTime pointInTime);
    Task<IEnumerable<DomainEvent>> GetAllEventsAsync();
}

/// <summary>
/// 快照存储接口
/// </summary>
public interface ISnapshotStore
{
    Task SaveSnapshotAsync(ISnapshot snapshot);
    Task<T?> GetSnapshotAsync<T>(string aggregateId) where T : class, ISnapshot;
    Task<SnapshotMetadata?> GetLatestSnapshotMetadataAsync<T>(string aggregateId) where T : class, ISnapshot;
    /// <summary>
    /// 获取指定 UTC 时间点之前的快照。
    /// </summary>
    Task<T?> GetSnapshotAsync<T>(string aggregateId, DateTime pointInTime) where T : class, ISnapshot;
}

/// <summary>
/// 聚合存储接口
/// </summary>
public interface IAggregateStore
{
    Task<T?> GetByIdAsync<T>(string id) where T : AggregateRoot;
    /// <summary>
    /// 获取聚合在指定 UTC 时间点的状态。
    /// </summary>
    Task<T?> GetByIdAsync<T>(string id, DateTime pointInTime) where T : AggregateRoot;
    Task SaveAsync<T>(T aggregate) where T : AggregateRoot;
}
