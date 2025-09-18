using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Infrastructure.Configurations;
using EventSourcingBankAccount.Domain.Aggregates;

namespace EventSourcingBankAccount.Infrastructure.Data;

/// <summary>
/// 事件存储数据模型
/// </summary>
public class EventStoreEntity
{
    public Guid Id { get; set; }
    public string AggregateId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 快照存储数据模型
/// </summary>
public class SnapshotEntity
{
    public Guid Id { get; set; }
    public string AggregateId { get; set; } = string.Empty;
    public string SnapshotType { get; set; } = string.Empty;
    public string SnapshotData { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 事件溯源数据库上下文
/// </summary>
public class EventSourcingDbContext : DbContext
{
    public EventSourcingDbContext(DbContextOptions<EventSourcingDbContext> options) : base(options)
    {
    }

    public DbSet<EventStoreEntity> Events { get; set; }
    public DbSet<SnapshotEntity> Snapshots { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用实体配置
        modelBuilder.ApplyConfiguration(new EventStoreEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SnapshotEntityConfiguration());
        modelBuilder.ApplyConfiguration(new BankAccountEntityConfiguration());
    }
}
