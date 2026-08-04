using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Exceptions;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// 事件存储实现
/// </summary>
public class EventStore : IEventStore
{
    private static readonly IReadOnlyDictionary<string, Type> EventTypes = typeof(DomainEvent).Assembly
        .GetTypes()
        .Where(t => !t.IsAbstract && typeof(DomainEvent).IsAssignableFrom(t))
        .ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);

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
            throw new ConcurrencyException(aggregateId, expectedVersion, lastVersion);
        }

        // 保存事件
        var eventEntities = eventsToSave.Select(e =>
        {
            e.AggregateId = aggregateId;
            return new EventStoreEntity
            {
                Id = e.EventId,
                AggregateId = aggregateId,
                EventType = e.EventType,
                EventData = JsonSerializer.Serialize((object)e, e.GetType(), _jsonOptions),
                Version = e.Version,
                Timestamp = EnsureUtc(e.Timestamp)
            };
        }).ToList();

        _context.Events.AddRange(eventEntities);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsEventVersionUniqueConstraintViolation(ex))
        {
            foreach (var entity in eventEntities)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }

            var actualVersion = await GetLatestVersionAsync(aggregateId);
            throw new ConcurrencyException(aggregateId, expectedVersion, actualVersion, ex);
        }
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, int fromVersion)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, DateTime pointInTime)
    {
        pointInTime = EnsureUtc(pointInTime);
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.Timestamp <= pointInTime)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    public async Task<int> GetLatestVersionAsync(string aggregateId)
    {
        return await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version) ?? 0;
    }

    public async Task<int> GetLatestVersionAsync(string aggregateId, DateTime pointInTime)
    {
        pointInTime = EnsureUtc(pointInTime);
        return await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.Timestamp <= pointInTime)
            .MaxAsync(e => (int?)e.Version) ?? 0;
    }

    public async Task<bool> ExistsAsync(string aggregateId)
    {
        return await _context.Events
            .AnyAsync(e => e.AggregateId == aggregateId);
    }

    public async Task<bool> ExistsAsync(string aggregateId, DateTime pointInTime)
    {
        pointInTime = EnsureUtc(pointInTime);
        return await _context.Events
            .AnyAsync(e => e.AggregateId == aggregateId && e.Timestamp <= pointInTime);
    }

    public async Task<IEnumerable<DomainEvent>> GetAllEventsAsync()
    {
        var eventEntities = await _context.Events
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    private DomainEvent DeserializeEvent(EventStoreEntity entity)
    {
        if (!EventTypes.TryGetValue(entity.EventType, out var eventType))
        {
            throw new EventDeserializationException(
                entity.AggregateId,
                entity.Version,
                entity.EventType,
                "未知或未登记的事件类型");
        }

        try
        {
            var @event = JsonSerializer.Deserialize(entity.EventData, eventType, _jsonOptions) as DomainEvent;
            return @event ?? throw new EventDeserializationException(
                entity.AggregateId,
                entity.Version,
                entity.EventType,
                "反序列化结果为空或不是 DomainEvent");
        }
        catch (JsonException ex)
        {
            throw new EventDeserializationException(
                entity.AggregateId,
                entity.Version,
                entity.EventType,
                "事件载荷不是有效 JSON 或与事件类型不匹配",
                ex);
        }
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

    private static bool IsEventVersionUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SqliteException sqliteException && IsSqliteEventVersionUniqueConstraint(sqliteException))
            {
                return true;
            }

            if (IsSqlServerEventVersionUniqueConstraint(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSqliteEventVersionUniqueConstraint(SqliteException exception)
    {
        if (exception.SqliteErrorCode != 19)
        {
            return false;
        }

        var message = exception.Message;
        return ContainsEventVersionIndex(message)
            || (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Events.AggregateId", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Events.Version", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSqlServerEventVersionUniqueConstraint(Exception exception)
    {
        var typeName = exception.GetType().FullName;
        if (typeName is not ("Microsoft.Data.SqlClient.SqlException" or "System.Data.SqlClient.SqlException"))
        {
            return false;
        }

        var numberValue = exception.GetType().GetProperty("Number")?.GetValue(exception);
        if (numberValue is not int number || number is not (2601 or 2627))
        {
            return false;
        }

        return ContainsEventVersionIndex(exception.Message)
            || (exception.Message.Contains("Events", StringComparison.OrdinalIgnoreCase)
                && exception.Message.Contains("AggregateId", StringComparison.OrdinalIgnoreCase)
                && exception.Message.Contains("Version", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsEventVersionIndex(string message)
    {
        return message.Contains("IX_Events_AggregateId_Version", StringComparison.OrdinalIgnoreCase);
    }
}
