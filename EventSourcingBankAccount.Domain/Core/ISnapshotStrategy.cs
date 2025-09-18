namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// 快照策略接口
/// </summary>
public interface ISnapshotStrategy
{
    bool ShouldCreateSnapshot(AggregateRoot aggregate);
}

/// <summary>
/// 基于事件数量的快照策略
/// </summary>
public class EventCountSnapshotStrategy : ISnapshotStrategy
{
    private readonly int _eventThreshold;
    
    public EventCountSnapshotStrategy(int eventThreshold = 10)
    {
        _eventThreshold = eventThreshold;
    }
    
    public bool ShouldCreateSnapshot(AggregateRoot aggregate)
    {
        return aggregate.Version > 0 && aggregate.Version % _eventThreshold == 0;
    }
}

/// <summary>
/// 基于时间间隔的快照策略
/// </summary>
public class TimeIntervalSnapshotStrategy : ISnapshotStrategy
{
    private readonly TimeSpan _interval;
    private readonly Dictionary<string, DateTime> _lastSnapshotTimes = new();
    
    public TimeIntervalSnapshotStrategy(TimeSpan interval)
    {
        _interval = interval;
    }
    
    public bool ShouldCreateSnapshot(AggregateRoot aggregate)
    {
        if (aggregate.Version == 0) return false;
        
        var now = DateTime.UtcNow;
        if (!_lastSnapshotTimes.ContainsKey(aggregate.Id))
        {
            _lastSnapshotTimes[aggregate.Id] = now;
            return true;
        }
        
        var shouldCreate = now - _lastSnapshotTimes[aggregate.Id] >= _interval;
        if (shouldCreate)
        {
            _lastSnapshotTimes[aggregate.Id] = now;
        }
        
        return shouldCreate;
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
    
    public bool ShouldCreateSnapshot(AggregateRoot aggregate)
    {
        return _strategies.Any(strategy => strategy.ShouldCreateSnapshot(aggregate));
    }
}