using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Domain.Events;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// 事件存储实现
/// </summary>
public class EventStore : IEventStore
{
    private readonly EventSourcingDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStore(EventSourcingDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task SaveEventsAsync(string aggregateId, IEnumerable<DomainEvent> events, int expectedVersion)
    {
        var eventsToSave = events.ToList();
        if (!eventsToSave.Any()) return;

        // 检查并发冲突
        var lastVersion = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version) ?? 0;

        if (lastVersion != expectedVersion)
        {
            throw new InvalidOperationException($"并发冲突：期望版本 {expectedVersion}，实际版本 {lastVersion}");
        }

        // 保存事件
        var eventEntities = eventsToSave.Select(e => new EventStoreEntity
        {
            Id = e.EventId,
            AggregateId = e.AggregateId,
            EventType = e.EventType,
            EventData = JsonSerializer.Serialize((object)e, e.GetType(), _jsonOptions),
            Version = e.Version,
            Timestamp = e.Timestamp
        });

        _context.Events.AddRange(eventEntities);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent).Where(e => e != null)!;
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, int fromVersion)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent).Where(e => e != null)!;
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, DateTime pointInTime)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.Timestamp <= pointInTime)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent).Where(e => e != null)!;
    }

    public async Task<IEnumerable<DomainEvent>> GetAllEventsAsync()
    {
        var eventEntities = await _context.Events
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent).Where(e => e != null)!;
    }

    private DomainEvent? DeserializeEvent(EventStoreEntity entity)
    {
        try
        {
            return entity.EventType switch
            {
                nameof(AccountCreated) => JsonSerializer.Deserialize<AccountCreated>(entity.EventData, _jsonOptions),
                nameof(MoneyDeposited) => JsonSerializer.Deserialize<MoneyDeposited>(entity.EventData, _jsonOptions),
                nameof(MoneyWithdrawn) => JsonSerializer.Deserialize<MoneyWithdrawn>(entity.EventData, _jsonOptions),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null; // 或者记录错误日志
        }
    }
}