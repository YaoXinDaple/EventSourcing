using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EventSourcingBankAccount.Infrastructure.Data;

namespace EventSourcingBankAccount.Infrastructure.Configurations;

/// <summary>
/// 事件存储实体配置
/// </summary>
public class EventStoreEntityConfiguration : IEntityTypeConfiguration<EventStoreEntity>
{
    public void Configure(EntityTypeBuilder<EventStoreEntity> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.AggregateId)
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("聚合根ID");
            
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("事件类型");
            
        builder.Property(e => e.EventData)
            .IsRequired()
            .HasComment("事件数据（JSON格式）");
            
        builder.Property(e => e.Version)
            .IsRequired()
            .HasComment("事件版本号");
            
        builder.Property(e => e.Timestamp)
            .IsRequired()
            .HasComment("事件时间戳");
        
        // 创建索引以提高查询性能
        builder.HasIndex(e => new { e.AggregateId, e.Version })
            .IsUnique()
            .HasDatabaseName("IX_Events_AggregateId_Version");
            
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_Events_Timestamp");
            
        builder.HasIndex(e => e.EventType)
            .HasDatabaseName("IX_Events_EventType");
    }
}

/// <summary>
/// 快照存储实体配置
/// </summary>
public class SnapshotEntityConfiguration : IEntityTypeConfiguration<SnapshotEntity>
{
    public void Configure(EntityTypeBuilder<SnapshotEntity> builder)
    {
        builder.ToTable("Snapshots");
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.AggregateId)
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("聚合根ID");
            
        builder.Property(s => s.SnapshotType)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("快照类型");
            
        builder.Property(s => s.SnapshotData)
            .IsRequired()
            .HasComment("快照数据（JSON格式）");
            
        builder.Property(s => s.Version)
            .IsRequired()
            .HasComment("快照版本号");
            
        builder.Property(s => s.Timestamp)
            .IsRequired()
            .HasComment("快照时间戳");
        
        // 创建索引以提高查询性能
        builder.HasIndex(s => new { s.AggregateId, s.Version })
            .IsUnique()
            .HasDatabaseName("IX_Snapshots_AggregateId_Version");
            
        builder.HasIndex(s => new { s.AggregateId, s.SnapshotType, s.Version })
            .HasDatabaseName("IX_Snapshots_AggregateId_Type_Version");
            
        builder.HasIndex(s => new { s.AggregateId, s.SnapshotType, s.Timestamp })
            .HasDatabaseName("IX_Snapshots_AggregateId_Type_Timestamp");
            
        builder.HasIndex(s => s.Timestamp)
            .HasDatabaseName("IX_Snapshots_Timestamp");
    }
}

public class BankAccountEntityConfiguration : IEntityTypeConfiguration<Domain.Aggregates.BankAccount>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.BankAccount> builder)
    {
        builder.ToTable("BankAccounts");
        builder.HasKey(b => b.Id);
        
        builder.Property(b => b.Id)
            .ValueGeneratedNever()
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("账户ID");
            
        builder.Property(b => b.AccountHolder)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("账户持有人姓名");
            
        builder.Property(b => b.Balance)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment("账户余额");
    }
}