namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// 快照策略接口
/// </summary>
public interface ISnapshotStrategy
{
    bool ShouldCreateSnapshot(AggregateRoot aggregate, SnapshotMetadata? latestSnapshot);
}

/// <summary>
/// 快照元信息
/// </summary>
public sealed record SnapshotMetadata(int Version, DateTime Timestamp);

/// <summary>
/// 基于事件数量的快照策略
/// </summary>
public class EventCountSnapshotStrategy : ISnapshotStrategy
{
    private readonly int _eventThreshold;
    
    public EventCountSnapshotStrategy(int eventThreshold = 10)
    {
        if (eventThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventThreshold), "事件阈值必须大于 0");

        _eventThreshold = eventThreshold;
    }
    
    public bool ShouldCreateSnapshot(AggregateRoot aggregate, SnapshotMetadata? latestSnapshot)
    {
        if (aggregate.Version == 0) return false;

        var latestSnapshotVersion = latestSnapshot?.Version ?? 0;
        return aggregate.Version > latestSnapshotVersion
            && aggregate.Version - latestSnapshotVersion >= _eventThreshold;
    }
}

/// <summary>
/// 基于时间间隔的快照策略
/// </summary>
public class TimeIntervalSnapshotStrategy : ISnapshotStrategy
{
    private readonly TimeSpan _interval;
    
    public TimeIntervalSnapshotStrategy(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "时间间隔必须大于 0");

        _interval = interval;
    }
    
    public bool ShouldCreateSnapshot(AggregateRoot aggregate, SnapshotMetadata? latestSnapshot)
    {
        if (aggregate.Version == 0) return false;

        return latestSnapshot == null || DateTime.UtcNow - latestSnapshot.Timestamp >= _interval;
    }
}

/// <summary>
/// 组合快照策略
/// </summary>
public class CompositeSnapshotStrategy : ISnapshotStrategy
{
    private readonly IList<ISnapshotStrategy> _strategies;
    
    public CompositeSnapshotStrategy(params ISnapshotStrategy[] strategies)
    {
        _strategies = strategies.ToList();
    }
    
    public bool ShouldCreateSnapshot(AggregateRoot aggregate, SnapshotMetadata? latestSnapshot)
    {
        return _strategies.Any(strategy => strategy.ShouldCreateSnapshot(aggregate, latestSnapshot));
    }
}
