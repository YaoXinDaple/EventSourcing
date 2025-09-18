using Microsoft.EntityFrameworkCore;
using EventSourcingBankAccount.Infrastructure.Configurations;
using EventSourcingBankAccount.Domain.Aggregates;

namespace EventSourcingBankAccount.Infrastructure.Data;

/// <summary>
/// �¼��洢����ģ��
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
/// ���մ洢����ģ��
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
/// �¼���Դ���ݿ�������
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

        // Ӧ��ʵ������
        modelBuilder.ApplyConfiguration(new EventStoreEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SnapshotEntityConfiguration());
        modelBuilder.ApplyConfiguration(new BankAccountEntityConfiguration());
    }
}
