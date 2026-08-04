using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// 聚合存储实现
/// </summary>
public class AggregateStore : IAggregateStore
{
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy _snapshotStrategy;
    private readonly EventSourcingDbContext _context;
    private readonly ILogger<AggregateStore> _logger;

    public AggregateStore(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        ISnapshotStrategy snapshotStrategy,
        EventSourcingDbContext context,
        ILogger<AggregateStore> logger)
    {
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
        _snapshotStrategy = snapshotStrategy;
        _context = context;
        _logger = logger;
    }

    public async Task<T?> GetByIdAsync<T>(string id) where T : AggregateRoot
    {
        return await GetByIdInternalAsync<T>(id);
    }

    public async Task<T?> GetByIdAsync<T>(string id, DateTime pointInTime) where T : AggregateRoot
    {
        return await GetByIdInternalAsync<T>(id, EnsureUtc(pointInTime));
    }

    public async Task SaveAsync<T>(T aggregate) where T : AggregateRoot
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        var uncommittedEvents = aggregate.GetUncommittedEvents();
        if (!uncommittedEvents.Any()) return;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 保存事件
            await _eventStore.SaveEventsAsync(aggregate.Id, uncommittedEvents, aggregate.Version);
            
            // 标记事件已提交
            aggregate.MarkEventsAsCommitted();

            // 检查是否需要创建快照
            if (aggregate is BankAccount bankAccount)
            {
                var latestSnapshot = await _snapshotStore.GetLatestSnapshotMetadataAsync<BankAccountSnapshot>(aggregate.Id);
                if (_snapshotStrategy.ShouldCreateSnapshot(aggregate, latestSnapshot))
                {
                    var snapshot = bankAccount.CreateSnapshot();
                    await _snapshotStore.SaveSnapshotAsync(snapshot);
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<T?> GetByIdInternalAsync<T>(string id, DateTime? pointInTime = null) where T : AggregateRoot
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        // 目前只支持BankAccount类型
        if (typeof(T) != typeof(BankAccount))
        {
            throw new NotSupportedException($"聚合类型 {typeof(T).Name} 尚未支持");
        }

        var eventStreamLatestVersion = pointInTime.HasValue
            ? await _eventStore.GetLatestVersionAsync(id, pointInTime.Value)
            : await _eventStore.GetLatestVersionAsync(id);
        if (eventStreamLatestVersion == 0)
        {
            return null;
        }

        // 使用反射创建一个空的 BankAccount 实例
        var bankAccount = CreateEmptyBankAccount();
        
        // 尝试从快照加载
        BankAccountSnapshot? snapshot = null;
        if (pointInTime.HasValue)
        {
            snapshot = await _snapshotStore.GetSnapshotAsync<BankAccountSnapshot>(id, pointInTime.Value);
        }
        else
        {
            snapshot = await _snapshotStore.GetSnapshotAsync<BankAccountSnapshot>(id);
        }

        if (snapshot != null)
        {
            if (snapshot.Version > eventStreamLatestVersion)
            {
                _logger.LogWarning(
                    "丢弃不可信快照。AggregateId: {AggregateId}, SnapshotVersion: {SnapshotVersion}, EventStreamLatestVersion: {EventStreamLatestVersion}",
                    id,
                    snapshot.Version,
                    eventStreamLatestVersion);
                snapshot = null;
            }
            else
            {
                bankAccount.LoadFromSnapshot(snapshot);
            }
        }

        // 加载快照之后的事件（或如果没有快照，则加载所有事件）
        IEnumerable<DomainEvent> events;
        if (pointInTime.HasValue)
        {
            events = await _eventStore.GetEventsAsync(id, pointInTime.Value);
            if (snapshot != null)
            {
                // 只加载快照版本之后的事件
                events = events.Where(e => e.Version > snapshot.Version);
            }
        }
        else
        {
            events = snapshot != null 
                ? await _eventStore.GetEventsAsync(id, snapshot.Version)
                : await _eventStore.GetEventsAsync(id);
        }

        if (events.Any())
        {
            bankAccount.LoadFromHistory(events);
        }

        return bankAccount as T;
    }

    private static BankAccount CreateEmptyBankAccount()
    {
        // 使用反射创建一个空的 BankAccount 实例，绕过构造函数
        return (BankAccount)Activator.CreateInstance(typeof(BankAccount), nonPublic: true)!;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
