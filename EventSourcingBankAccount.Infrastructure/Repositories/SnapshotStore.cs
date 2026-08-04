using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Exceptions;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Domain.Aggregates;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// 快照存储实现
/// </summary>
public class SnapshotStore : ISnapshotStore
{
    private readonly EventSourcingDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public SnapshotStore(EventSourcingDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task SaveSnapshotAsync(ISnapshot snapshot)
    {
        var entity = new SnapshotEntity
        {
            Id = Guid.NewGuid(),
            AggregateId = snapshot.AggregateId,
            SnapshotType = snapshot.GetType().Name,
            SnapshotData = JsonSerializer.Serialize((object)snapshot, snapshot.GetType(), _jsonOptions),
            Version = snapshot.Version,
            Timestamp = EnsureUtc(snapshot.Timestamp)
        };

        _context.Snapshots.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<T?> GetSnapshotAsync<T>(string aggregateId) where T : class, ISnapshot
    {
        var entity = await _context.Snapshots
            .Where(s => s.AggregateId == aggregateId && s.SnapshotType == typeof(T).Name)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        return DeserializeSnapshot<T>(entity);
    }

    public async Task<SnapshotMetadata?> GetLatestSnapshotMetadataAsync<T>(string aggregateId) where T : class, ISnapshot
    {
        var metadata = await _context.Snapshots
            .Where(s => s.AggregateId == aggregateId && s.SnapshotType == typeof(T).Name)
            .OrderByDescending(s => s.Version)
            .Select(s => new { s.Version, s.Timestamp })
            .FirstOrDefaultAsync();

        return metadata == null
            ? null
            : new SnapshotMetadata(metadata.Version, EnsureUtc(metadata.Timestamp));
    }

    public async Task<T?> GetSnapshotAsync<T>(string aggregateId, DateTime pointInTime) where T : class, ISnapshot
    {
        pointInTime = EnsureUtc(pointInTime);
        var entity = await _context.Snapshots
            .Where(s => s.AggregateId == aggregateId 
                       && s.SnapshotType == typeof(T).Name 
                       && s.Timestamp <= pointInTime)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        return DeserializeSnapshot<T>(entity);
    }

    private T DeserializeSnapshot<T>(SnapshotEntity entity) where T : class, ISnapshot
    {
        try
        {
            return JsonSerializer.Deserialize<T>(entity.SnapshotData, _jsonOptions)
                ?? throw new SnapshotDeserializationException(
                    entity.AggregateId,
                    entity.Version,
                    entity.SnapshotType,
                    "反序列化结果为空");
        }
        catch (JsonException ex)
        {
            throw new SnapshotDeserializationException(
                entity.AggregateId,
                entity.Version,
                entity.SnapshotType,
                "快照载荷不是有效 JSON 或与快照类型不匹配",
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
}
