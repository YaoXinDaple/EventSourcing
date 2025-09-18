using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// �ۺϴ洢ʵ��
/// </summary>
public class AggregateStore : IAggregateStore
{
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy _snapshotStrategy;
    private readonly EventSourcingDbContext _context;

    public AggregateStore(IEventStore eventStore, ISnapshotStore snapshotStore, ISnapshotStrategy snapshotStrategy, EventSourcingDbContext context)
    {
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
        _snapshotStrategy = snapshotStrategy;
        _context = context;
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
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        var uncommittedEvents = aggregate.GetUncommittedEvents();
        if (!uncommittedEvents.Any()) return;

        // �����¼�
        await _eventStore.SaveEventsAsync(aggregate.Id, uncommittedEvents, aggregate.Version);
        
        // ����¼����ύ
        aggregate.MarkEventsAsCommitted();

        // ����Ƿ���Ҫ��������
        if (_snapshotStrategy.ShouldCreateSnapshot(aggregate) && aggregate is BankAccount bankAccount)
        {
            var snapshot = bankAccount.CreateSnapshot();
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

        return bankAccount as T;
    }

    private static BankAccount CreateEmptyBankAccount()
    {
        // ʹ�÷��䴴��һ���յ� BankAccount ʵ�����ƹ����캯��
        return (BankAccount)Activator.CreateInstance(typeof(BankAccount), nonPublic: true)!;
    }
}