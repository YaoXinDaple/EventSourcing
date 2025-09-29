using AsyncKeyedLock;
using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// �ۺϴ洢ʵ�֣����ֲ�ʽ���棩
/// </summary>
public class AggregateStore : IAggregateStore
{
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy _snapshotStrategy;
    private readonly IDistributedCache _cache; 
    private static readonly AsyncKeyedLocker<string> _asyncKeyedLocker = new();
    private readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(5)
    };
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AggregateStore(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        ISnapshotStrategy snapshotStrategy,
        IDistributedCache cache)
    {
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
        _snapshotStrategy = snapshotStrategy;
        _cache = cache;

    }

    public async Task<T?> GetByIdAsync<T>(string id) where T : AggregateRoot
    {
        return await GetByIdInternalAsync<T>(id);
    }

    public async Task<T?> GetByIdAsync<T>(string id, DateTime pointInTime) where T : AggregateRoot
    {
        return await GetByIdInternalAsync<T>(id, pointInTime);
    }

    public async Task SaveAsync<T>(T aggregate) where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var uncommittedEvents = aggregate.GetUncommittedEvents();
        if (!uncommittedEvents.Any()) return;

        // �����¼�������������飩
        await _eventStore.SaveEventsAsync(aggregate.Id, uncommittedEvents, aggregate.Version);

        // ����¼����ύ�����°汾��
        aggregate.MarkEventsAsCommitted();

        // ����Ƿ���Ҫ�������գ��־û���
        if (_snapshotStrategy.ShouldCreateSnapshot(aggregate) && aggregate is BankAccount bankAccountForPersist)
        {
            var snapshot = bankAccountForPersist.CreateSnapshot();
            await _snapshotStore.SaveSnapshotAsync(snapshot);
        }
    }

    private async Task<T?> GetByIdInternalAsync<T>(string id, DateTime? pointInTime = null) where T : AggregateRoot
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        // Ŀǰֻ֧��BankAccount����
        if (typeof(T) != typeof(BankAccount))
        {
            throw new NotSupportedException($"�ۺ����� {typeof(T).Name} ��δ֧��");
        }

        // �Ե�ǰ����״̬�Ĳ�ѯ���Զ�ȡ���棬ʱ����ѯ���ƹ�����
        if (!pointInTime.HasValue)
        {
            var cachedJson = await _cache.GetStringAsync(GetCacheKey<BankAccount>(id));
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedAgg = JsonSerializer.Deserialize<BankAccount>(cachedJson, _jsonOptions);
                if (cachedAgg != null)
                {
                    return cachedAgg as T;
                }
            }
        }

        // ʹ�÷��䴴��һ���յ� BankAccount ʵ��
        var bankAccount = CreateEmptyBankAccount();

        // ���Դӿ��ռ���
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
            bankAccount.LoadFromSnapshot(snapshot);
        }

        // ���ؿ���֮����¼��������û�п��գ�����������¼���
        IEnumerable<DomainEvent> events;
        if (pointInTime.HasValue)
        {
            events = await _eventStore.GetEventsAsync(id, pointInTime.Value);
            if (snapshot != null)
            {
                // ֻ���ؿ��հ汾֮����¼�
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

        // �����û�п���Ҳû���¼���˵���ۺϲ�����
        if (snapshot == null && !events.Any())
        {
            return null;
        }

        // ������״̬��ѯ�����سɹ���д�뻺��
        if (!pointInTime.HasValue)
        {
            var json = JsonSerializer.Serialize(bankAccount, _jsonOptions);
            await _cache.SetStringAsync(GetCacheKey<BankAccount>(id), json, _cacheOptions);
        }

        return bankAccount as T;
    }

    private static string GetCacheKey<TAgg>(string id) => $"agg:{typeof(TAgg).Name}:{id}";

    private static BankAccount CreateEmptyBankAccount()
    {
        // ʹ�÷��䴴��һ���յ� BankAccount ʵ�����ƹ����캯��
        return (BankAccount)Activator.CreateInstance(typeof(BankAccount), nonPublic: true)!;
    }
}
