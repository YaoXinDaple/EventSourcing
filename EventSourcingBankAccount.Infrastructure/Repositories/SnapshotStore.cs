using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Domain.Core;
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
            Timestamp = snapshot.Timestamp
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

    public async Task<T?> GetSnapshotAsync<T>(string aggregateId, DateTime pointInTime) where T : class, ISnapshot
    {
        var entity = await _context.Snapshots
            .Where(s => s.AggregateId == aggregateId 
                       && s.SnapshotType == typeof(T).Name 
                       && s.Timestamp <= pointInTime)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        return DeserializeSnapshot<T>(entity);
    }

    private T? DeserializeSnapshot<T>(SnapshotEntity entity) where T : class, ISnapshot
    {
        try
        {
            return JsonSerializer.Deserialize<T>(entity.SnapshotData, _jsonOptions);
        }
        catch (JsonException)
        {
            return null; // 或者记录错误日志
        }
    }
}