using System.Text.Json;
using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Domain.Exceptions;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Domain.Queries;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Infrastructure.Handlers;
using EventSourcingBankAccount.Infrastructure.Repositories;
using EventSourcingBankAccount.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventSourcingBankAccount.Tests.Infrastructure;

public class AggregateStoreTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldReplayEventsAndRestoreHistoricalTimestamps()
    {
        // 回归：重放后 CreatedAt / LastModifiedAt 曾使用当前时刻，而不是事件中的历史时间。
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-replay";
        var createdAt = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var depositedAt = new DateTime(2024, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var withdrawnAt = new DateTime(2024, 1, 3, 8, 0, 0, DateTimeKind.Utc);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, createdAt),
            new MoneyDeposited(aggregateId, 50m, 150m, "deposit").WithMetadata(aggregateId, 2, depositedAt),
            new MoneyWithdrawn(aggregateId, 20m, 130m, "withdraw").WithMetadata(aggregateId, 3, withdrawnAt));

        var account = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId);

        Assert.NotNull(account);
        Assert.Equal(130m, account.Balance);
        Assert.Equal(3, account.Version);
        Assert.Equal(createdAt, account.CreatedAt);
        Assert.Equal(withdrawnAt, account.LastModifiedAt);
    }

    [Fact]
    public async Task SaveAsync_WhenEventCountIsBelowSnapshotThreshold_ShouldNotCreateSnapshot()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var account = new BankAccount("acct-no-snapshot", "Alice", 0m);
        for (var i = 0; i < 8; i++)
        {
            account.Deposit(10m);
        }

        await host.AggregateStore.SaveAsync(account);

        Assert.Equal(0, await host.Context.Snapshots.CountAsync());
        Assert.Equal(9, account.Version);
    }

    [Fact]
    public async Task SaveAsync_WhenEventCountReachesDefaultThreshold_ShouldCreateSnapshotAtVersion10()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var account = new BankAccount("acct-snapshot", "Alice", 0m);
        for (var i = 0; i < 9; i++)
        {
            account.Deposit(10m);
        }

        await host.AggregateStore.SaveAsync(account);

        var snapshot = await host.Context.Snapshots.SingleAsync();
        Assert.Equal("acct-snapshot", snapshot.AggregateId);
        Assert.Equal(10, snapshot.Version);
    }

    [Fact]
    public async Task SaveAsync_WithDefaultThresholdAcrossContinuousOperations_ShouldCreateSnapshotsAtVersion10And20()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var account = new BankAccount("acct-snapshot-continuous", "Alice", 0m);
        for (var i = 0; i < 9; i++)
        {
            account.Deposit(10m);
        }

        await host.AggregateStore.SaveAsync(account);

        for (var i = 0; i < 10; i++)
        {
            account.Deposit(10m);
        }

        await host.AggregateStore.SaveAsync(account);

        var snapshotVersions = await host.Context.Snapshots
            .Where(s => s.AggregateId == "acct-snapshot-continuous")
            .OrderBy(s => s.Version)
            .Select(s => s.Version)
            .ToListAsync();
        Assert.Equal(new[] { 10, 20 }, snapshotVersions);
    }

    [Fact]
    public void TimeIntervalSnapshotStrategy_WithNewInstancesAndIntervalNotElapsed_ShouldReturnFalse()
    {
        var aggregate = CreateCommittedAccount("acct-time-interval-scoped", version: 2);
        var latestSnapshot = new SnapshotMetadata(1, DateTime.UtcNow);

        var firstResolvedStrategy = new TimeIntervalSnapshotStrategy(TimeSpan.FromMinutes(10));
        var secondResolvedStrategy = new TimeIntervalSnapshotStrategy(TimeSpan.FromMinutes(10));

        Assert.False(firstResolvedStrategy.ShouldCreateSnapshot(aggregate, latestSnapshot));
        Assert.False(secondResolvedStrategy.ShouldCreateSnapshot(aggregate, latestSnapshot));
    }

    [Fact]
    public void TimeIntervalSnapshotStrategy_WhenNoSnapshotExists_ShouldReturnTrue()
    {
        var aggregate = CreateCommittedAccount("acct-time-interval-first", version: 1);
        var strategy = new TimeIntervalSnapshotStrategy(TimeSpan.FromMinutes(10));

        Assert.True(strategy.ShouldCreateSnapshot(aggregate, latestSnapshot: null));
    }

    [Fact]
    public void EventCountSnapshotStrategy_WhenEventsSinceLastSnapshotReachThreshold_ShouldReturnTrue()
    {
        var aggregate = CreateCommittedAccount("acct-event-count-hit", version: 15);
        var strategy = new EventCountSnapshotStrategy(eventThreshold: 5);

        Assert.True(strategy.ShouldCreateSnapshot(
            aggregate,
            new SnapshotMetadata(10, DateTime.UtcNow.AddMinutes(-1))));
    }

    [Fact]
    public void EventCountSnapshotStrategy_WhenEventsSinceLastSnapshotAreBelowThreshold_ShouldReturnFalse()
    {
        var aggregate = CreateCommittedAccount("acct-event-count-miss", version: 14);
        var strategy = new EventCountSnapshotStrategy(eventThreshold: 5);

        Assert.False(strategy.ShouldCreateSnapshot(
            aggregate,
            new SnapshotMetadata(10, DateTime.UtcNow.AddMinutes(-1))));
    }

    [Fact]
    public void EventCountSnapshotStrategy_WhenLastSnapshotVersionIsNotThresholdMultiple_ShouldStillTrigger()
    {
        var aggregate = CreateCommittedAccount("acct-event-count-non-multiple", version: 12);
        var strategy = new EventCountSnapshotStrategy(eventThreshold: 5);

        Assert.True(strategy.ShouldCreateSnapshot(
            aggregate,
            new SnapshotMetadata(7, DateTime.UtcNow.AddMinutes(-1))));
    }

    [Fact]
    public void CompositeSnapshotStrategy_WhenAnyStrategyMatches_ShouldReturnTrue()
    {
        var aggregate = CreateCommittedAccount("acct-composite", version: 2);
        var strategy = new CompositeSnapshotStrategy(
            new EventCountSnapshotStrategy(eventThreshold: 10),
            new TimeIntervalSnapshotStrategy(TimeSpan.FromMinutes(10)));

        Assert.True(strategy.ShouldCreateSnapshot(aggregate, latestSnapshot: null));
    }

    [Fact]
    public async Task GetByIdAsync_AcrossSnapshotBoundary_ShouldReturnExpectedBalance()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var account = new BankAccount("acct-cross-snapshot", "Alice", 0m);
        for (var i = 0; i < 9; i++)
        {
            account.Deposit(10m);
        }

        await host.AggregateStore.SaveAsync(account);
        account.Deposit(25m);
        account.Withdraw(5m);
        await host.AggregateStore.SaveAsync(account);

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>("acct-cross-snapshot");

        Assert.NotNull(loaded);
        Assert.Equal(110m, loaded.Balance);
        Assert.Equal(12, loaded.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoEventsAfterSnapshot_ShouldLoadSnapshotOnly()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var account = new BankAccount("acct-snapshot-only", "Alice", 0m);
        for (var i = 0; i < 9; i++)
        {
            account.Deposit(10m);
        }

        await host.AggregateStore.SaveAsync(account);

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>("acct-snapshot-only");

        Assert.NotNull(loaded);
        Assert.Equal(90m, loaded.Balance);
        Assert.Equal(10, loaded.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOnlyOrphanSnapshotExists_ShouldReturnNull()
    {
        // 回归：只有孤儿快照、没有任何事件时，系统曾加载出幽灵账户。
        await using var host = await EventSourcingTestHost.CreateAsync();
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = "acct-orphan",
            Version = 10,
            Timestamp = DateTime.UtcNow,
            AccountHolder = "Ghost",
            Balance = 999m,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });

        var account = await host.AggregateStore.GetByIdAsync<BankAccount>("acct-orphan");

        Assert.Null(account);
    }

    [Fact]
    public async Task SaveAsync_WhenOnlyOrphanSnapshotExists_ShouldAllowCreatingAccount()
    {
        // 回归：孤儿快照不应阻止之后用同一个账户 ID 正常建户。
        await using var host = await EventSourcingTestHost.CreateAsync();
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = "acct-orphan-create",
            Version = 10,
            Timestamp = DateTime.UtcNow,
            AccountHolder = "Ghost",
            Balance = 999m,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });

        var account = new BankAccount("acct-orphan-create", "Alice", 25m);
        await host.AggregateStore.SaveAsync(account);

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>("acct-orphan-create");
        Assert.NotNull(loaded);
        Assert.Equal(25m, loaded.Balance);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSnapshotVersionIsGreaterThanEventStreamLatestVersion_ShouldDiscardSnapshotAndReplayAllEvents()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-stale-snapshot";
        var createdAt = new DateTime(2024, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var depositedAt = createdAt.AddHours(1);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 25m).WithMetadata(aggregateId, 1, createdAt),
            new MoneyDeposited(aggregateId, 10m, 35m, "deposit").WithMetadata(aggregateId, 2, depositedAt));
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = aggregateId,
            Version = 10,
            Timestamp = depositedAt.AddMinutes(1),
            AccountHolder = "Ghost",
            Balance = 999m,
            CreatedAt = createdAt,
            LastModifiedAt = depositedAt
        });

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded.AccountHolder);
        Assert.Equal(35m, loaded.Balance);
        Assert.Equal(2, loaded.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSnapshotVersionEqualsEventStreamLatestVersion_ShouldLoadSnapshotOnly()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-current-snapshot";
        var createdAt = new DateTime(2024, 4, 2, 8, 0, 0, DateTimeKind.Utc);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 25m).WithMetadata(aggregateId, 1, createdAt));
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = aggregateId,
            Version = 1,
            Timestamp = createdAt,
            AccountHolder = "Alice",
            Balance = 25m,
            CreatedAt = createdAt,
            LastModifiedAt = createdAt
        });

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded.AccountHolder);
        Assert.Equal(25m, loaded.Balance);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSnapshotVersionIsLessThanEventStreamLatestVersion_ShouldLoadSnapshotAndIncrementalEvents()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-incremental-snapshot";
        var createdAt = new DateTime(2024, 4, 3, 8, 0, 0, DateTimeKind.Utc);
        var depositedAt = createdAt.AddHours(1);
        var withdrawnAt = createdAt.AddHours(2);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, createdAt),
            new MoneyDeposited(aggregateId, 50m, 150m, "deposit").WithMetadata(aggregateId, 2, depositedAt),
            new MoneyWithdrawn(aggregateId, 20m, 130m, "withdraw").WithMetadata(aggregateId, 3, withdrawnAt));
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = aggregateId,
            Version = 2,
            Timestamp = depositedAt,
            AccountHolder = "Alice",
            Balance = 150m,
            CreatedAt = createdAt,
            LastModifiedAt = depositedAt
        });

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId);

        Assert.NotNull(loaded);
        Assert.Equal(130m, loaded.Balance);
        Assert.Equal(3, loaded.Version);
        Assert.Equal(withdrawnAt, loaded.LastModifiedAt);
    }

    [Fact]
    public async Task GetByIdAsync_AtHistoricalPoint_WhenSnapshotVersionIsGreaterThanHistoricalEventStreamLatestVersion_ShouldDiscardSnapshotAndReplayAllHistoricalEvents()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-stale-historical-snapshot";
        var createdAt = new DateTime(2024, 4, 4, 8, 0, 0, DateTimeKind.Utc);
        var pointInTime = createdAt.AddMinutes(30);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 25m).WithMetadata(aggregateId, 1, createdAt));
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = aggregateId,
            Version = 10,
            Timestamp = createdAt.AddMinutes(10),
            AccountHolder = "Ghost",
            Balance = 999m,
            CreatedAt = createdAt,
            LastModifiedAt = createdAt.AddMinutes(10)
        });

        var loaded = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId, pointInTime);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded.AccountHolder);
        Assert.Equal(25m, loaded.Balance);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task GetByIdAsync_AtPointBeforeCreation_ShouldReturnNull()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-before";
        var createdAt = new DateTime(2024, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        await InsertEventsAsync(host, new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, createdAt));

        var account = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId, createdAt.AddTicks(-1));

        Assert.Null(account);
    }

    [Fact]
    public async Task GetByIdAsync_AtHistoricalPoint_ShouldReturnBalanceAtThatTime()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-point-in-time";
        var createdAt = new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var firstDepositAt = createdAt.AddHours(1);
        var withdrawAt = createdAt.AddHours(2);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, createdAt),
            new MoneyDeposited(aggregateId, 40m, 140m, "deposit").WithMetadata(aggregateId, 2, firstDepositAt),
            new MoneyWithdrawn(aggregateId, 15m, 125m, "withdraw").WithMetadata(aggregateId, 3, withdrawAt));

        var historical = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId, firstDepositAt);
        var latest = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId);

        Assert.NotNull(historical);
        Assert.NotNull(latest);
        Assert.Equal(140m, historical.Balance);
        Assert.Equal(2, historical.Version);
        Assert.Equal(125m, latest.Balance);
        Assert.Equal(3, latest.Version);
    }

    [Fact]
    public async Task GetAccountStateAtTime_WithPlusEightOffsetAndUtcInstant_ShouldReturnSameResult()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-offset-equivalent";
        var createdAt = new DateTime(2026, 8, 4, 1, 50, 0, DateTimeKind.Utc);
        var depositedAt = new DateTime(2026, 8, 4, 2, 0, 0, DateTimeKind.Utc);
        var withdrawnAt = new DateTime(2026, 8, 4, 2, 10, 0, DateTimeKind.Utc);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, createdAt),
            new MoneyDeposited(aggregateId, 50m, 150m, "deposit").WithMetadata(aggregateId, 2, depositedAt),
            new MoneyWithdrawn(aggregateId, 25m, 125m, "withdraw").WithMetadata(aggregateId, 3, withdrawnAt));

        var handler = new GetAccountStateAtTimeHandler(host.AggregateStore);
        var plusEightInstant = new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.FromHours(8));
        var utcInstant = new DateTimeOffset(2026, 8, 4, 2, 0, 0, TimeSpan.Zero);

        var plusEightResult = await handler.HandleAsync(new GetAccountStateAtTime(aggregateId, plusEightInstant.UtcDateTime));
        var utcResult = await handler.HandleAsync(new GetAccountStateAtTime(aggregateId, utcInstant.UtcDateTime));

        Assert.Equal(utcResult.AccountId, plusEightResult.AccountId);
        Assert.Equal(utcResult.Balance, plusEightResult.Balance);
        Assert.Equal(utcResult.AccountHolder, plusEightResult.AccountHolder);
        Assert.Equal(utcResult.StateAtTime, plusEightResult.StateAtTime);
        Assert.Equal(utcResult.CreatedAt, plusEightResult.CreatedAt);
        Assert.Equal(utcResult.LastModifiedAt, plusEightResult.LastModifiedAt);
        Assert.Equal(utcResult.Version, plusEightResult.Version);
        Assert.Equal(TimeSpan.Zero, plusEightResult.StateAtTime.Offset);
        Assert.Equal(150m, plusEightResult.Balance);
        Assert.Equal(2, plusEightResult.Version);
    }

    [Fact]
    public async Task GetByIdAsync_AtHistoricalPointAcrossTimezoneBoundary_ShouldReturnHistoricalBalance()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-timezone-boundary";
        var createdAt = new DateTime(2026, 8, 3, 23, 55, 0, DateTimeKind.Utc);
        var depositedAt = new DateTime(2026, 8, 4, 0, 5, 0, DateTimeKind.Utc);
        var withdrawnAt = new DateTime(2026, 8, 4, 0, 15, 0, DateTimeKind.Utc);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, createdAt),
            new MoneyDeposited(aggregateId, 40m, 140m, "deposit").WithMetadata(aggregateId, 2, depositedAt),
            new MoneyWithdrawn(aggregateId, 15m, 125m, "withdraw").WithMetadata(aggregateId, 3, withdrawnAt));

        var pointInTime = new DateTimeOffset(2026, 8, 4, 8, 5, 0, TimeSpan.FromHours(8)).UtcDateTime;
        var historical = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId, pointInTime);
        var latest = await host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId);

        Assert.NotNull(historical);
        Assert.NotNull(latest);
        Assert.Equal(140m, historical.Balance);
        Assert.Equal(2, historical.Version);
        Assert.Equal(125m, latest.Balance);
        Assert.Equal(3, latest.Version);
    }

    [Fact]
    public async Task StoredTimestamps_WhenReadBackFromSqlite_ShouldBeMarkedUtc()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-timestamp-kind";
        var timestamp = DateTime.SpecifyKind(new DateTime(2026, 8, 4, 2, 0, 0), DateTimeKind.Utc);

        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, timestamp));
        await host.SnapshotStore.SaveSnapshotAsync(new BankAccountSnapshot
        {
            AggregateId = aggregateId,
            Version = 1,
            Timestamp = timestamp,
            AccountHolder = "Alice",
            Balance = 100m,
            CreatedAt = timestamp,
            LastModifiedAt = timestamp
        });

        host.Context.ChangeTracker.Clear();
        var eventEntity = await host.Context.Events.AsNoTracking().SingleAsync(e => e.AggregateId == aggregateId);
        var snapshotEntity = await host.Context.Snapshots.AsNoTracking().SingleAsync(s => s.AggregateId == aggregateId);

        Assert.Equal(DateTimeKind.Utc, eventEntity.Timestamp.Kind);
        Assert.Equal(DateTimeKind.Utc, snapshotEntity.Timestamp.Kind);
        Assert.Equal(timestamp, eventEntity.Timestamp);
        Assert.Equal(timestamp, snapshotEntity.Timestamp);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEventDataIsInvalidJson_ShouldThrowEventDeserializationException()
    {
        // 回归：损坏事件数据曾被静默跳过，导致聚合状态看似成功但实际错误。
        await using var host = await EventSourcingTestHost.CreateAsync();
        await InsertRawEventAsync(host, "acct-bad-json", "MoneyDeposited", "{not json", 1, DateTime.UtcNow);

        var exception = await Assert.ThrowsAsync<EventDeserializationException>(
            () => host.AggregateStore.GetByIdAsync<BankAccount>("acct-bad-json"));

        Assert.Contains("acct-bad-json", exception.Message);
        Assert.Contains("Version: 1", exception.Message);
        Assert.Contains("MoneyDeposited", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEventTypeIsUnknown_ShouldThrowEventDeserializationException()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        await InsertRawEventAsync(host, "acct-unknown-event", "UnknownEvent", "{}", 1, DateTime.UtcNow);

        var exception = await Assert.ThrowsAsync<EventDeserializationException>(
            () => host.AggregateStore.GetByIdAsync<BankAccount>("acct-unknown-event"));

        Assert.Contains("acct-unknown-event", exception.Message);
        Assert.Contains("Version: 1", exception.Message);
        Assert.Contains("UnknownEvent", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWithdrawEventIsCorrupt_ShouldThrowInsteadOfReturningInflatedBalance()
    {
        // 回归：损坏的取款事件不能被静默丢弃，否则余额会偏高。
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateId = "acct-corrupt-withdraw";
        await InsertEventsAsync(
            host,
            new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, DateTime.UtcNow.AddMinutes(-3)),
            new MoneyDeposited(aggregateId, 50m, 150m, "deposit").WithMetadata(aggregateId, 2, DateTime.UtcNow.AddMinutes(-2)));
        await InsertRawEventAsync(host, aggregateId, "MoneyWithdrawn", "{not json", 3, DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<EventDeserializationException>(
            () => host.AggregateStore.GetByIdAsync<BankAccount>(aggregateId));
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSnapshotDataIsInvalidJson_ShouldThrowSnapshotDeserializationException()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        host.Context.Snapshots.Add(new SnapshotEntity
        {
            Id = Guid.NewGuid(),
            AggregateId = "acct-bad-snapshot",
            SnapshotType = nameof(BankAccountSnapshot),
            SnapshotData = "{not json",
            Version = 10,
            Timestamp = DateTime.UtcNow
        });
        await host.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SnapshotDeserializationException>(
            () => host.SnapshotStore.GetSnapshotAsync<BankAccountSnapshot>("acct-bad-snapshot"));

        Assert.Contains("acct-bad-snapshot", exception.Message);
        Assert.Contains("Version: 10", exception.Message);
        Assert.Contains(nameof(BankAccountSnapshot), exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSnapshotDoesNotExist_ShouldReturnNull()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();

        var snapshot = await host.SnapshotStore.GetSnapshotAsync<BankAccountSnapshot>("acct-missing-snapshot");

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task SaveEventsAsync_WithSameExpectedVersionTwice_ShouldThrowConcurrencyException()
    {
        // 回归：并发写入必须抛出可识别的并发异常，而不是 InvalidOperationException 或裸 DbUpdateException。
        await using var host = await EventSourcingTestHost.CreateAsync();
        var firstBatch = new[]
        {
            new AccountCreated("acct-concurrency", "Alice", 100m).WithMetadata("acct-concurrency", 1, DateTime.UtcNow)
        };
        var secondBatch = new[]
        {
            new AccountCreated("acct-concurrency", "Alice", 200m).WithMetadata("acct-concurrency", 1, DateTime.UtcNow)
        };

        await host.EventStore.SaveEventsAsync("acct-concurrency", firstBatch, expectedVersion: 0);

        var exception = await Assert.ThrowsAsync<ConcurrencyException>(
            () => host.EventStore.SaveEventsAsync("acct-concurrency", secondBatch, expectedVersion: 0));

        Assert.Equal("acct-concurrency", exception.AggregateId);
        Assert.Equal(0, exception.ExpectedVersion);
        Assert.Equal(1, exception.ActualVersion);
    }

    [Fact]
    public async Task SaveAsync_WithTwoAggregateStoresUsingSameExpectedVersion_ShouldThrowConcurrencyException()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        await host.AggregateStore.SaveAsync(new BankAccount("acct-two-stores", "Alice", 100m));

        await using var firstContext = EventSourcingTestHost.CreateContext(host.DatabasePath);
        await using var secondContext = EventSourcingTestHost.CreateContext(host.DatabasePath);
        var firstStore = CreateAggregateStore(firstContext);
        var secondStore = CreateAggregateStore(secondContext);

        var first = await firstStore.GetByIdAsync<BankAccount>("acct-two-stores");
        var second = await secondStore.GetByIdAsync<BankAccount>("acct-two-stores");
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.Deposit(10m, "first");
        second.Deposit(20m, "second");

        await firstStore.SaveAsync(first);
        var exception = await Assert.ThrowsAsync<ConcurrencyException>(() => secondStore.SaveAsync(second));

        Assert.Equal("acct-two-stores", exception.AggregateId);
        Assert.Equal(1, exception.ExpectedVersion);
        Assert.Equal(2, exception.ActualVersion);
    }

    [Fact]
    public async Task SaveEventsAsync_WhenUniqueIndexFailsAfterVersionCheck_ShouldThrowConcurrencyException()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"eventsourcing-tests-{Guid.NewGuid():N}.db");
        var aggregateId = "acct-unique-race";
        var interceptor = new InsertDuplicateEventBeforeSaveInterceptor(databasePath, aggregateId);
        var options = new DbContextOptionsBuilder<EventSourcingDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .AddInterceptors(interceptor)
            .Options;

        try
        {
            await using (var setupContext = new EventSourcingDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();
            }

            await using var context = new EventSourcingDbContext(options);
            var eventStore = new EventStore(context);
            var events = new[]
            {
                new AccountCreated(aggregateId, "Alice", 100m).WithMetadata(aggregateId, 1, DateTime.UtcNow)
            };

            var exception = await Assert.ThrowsAsync<ConcurrencyException>(
                () => eventStore.SaveEventsAsync(aggregateId, events, expectedVersion: 0));

            Assert.Equal(aggregateId, exception.AggregateId);
            Assert.Equal(0, exception.ExpectedVersion);
            Assert.Equal(1, exception.ActualVersion);
            Assert.IsType<DbUpdateException>(exception.InnerException);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    [Fact]
    public async Task SaveAsync_WhenSnapshotSaveFails_ShouldRollbackSavedEvents()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var aggregateStore = new AggregateStore(
            host.EventStore,
            new ThrowingSnapshotStore(),
            new EventCountSnapshotStrategy(eventThreshold: 1),
            host.Context,
            NullLogger<AggregateStore>.Instance);
        var account = new BankAccount("acct-atomicity", "Alice", 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => aggregateStore.SaveAsync(account));

        Assert.Equal(0, await host.Context.Events.CountAsync(e => e.AggregateId == "acct-atomicity"));
        Assert.Equal(0, await host.Context.Snapshots.CountAsync(e => e.AggregateId == "acct-atomicity"));
    }

    [Fact]
    public async Task SaveEventsAsync_WhenDatabaseErrorIsNotConcurrency_ShouldThrowOriginalDbUpdateException()
    {
        await using var host = await EventSourcingTestHost.CreateAsync();
        var events = new[]
        {
            new AccountCreated("acct-db-error", "Alice", 100m).WithMetadata("acct-db-error", 1, DateTime.UtcNow)
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => host.EventStore.SaveEventsAsync(null!, events, expectedVersion: 0));
    }

    private static async Task InsertEventsAsync(EventSourcingTestHost host, params DomainEvent[] events)
    {
        foreach (var @event in events)
        {
            await InsertRawEventAsync(
                host,
                @event.AggregateId,
                @event.EventType,
                EventTestData.Serialize(@event),
                @event.Version,
                @event.Timestamp,
                @event.EventId);
        }
    }

    private static async Task InsertRawEventAsync(
        EventSourcingTestHost host,
        string aggregateId,
        string eventType,
        string eventData,
        int version,
        DateTime timestamp,
        Guid? eventId = null)
    {
        host.Context.Events.Add(new EventStoreEntity
        {
            Id = eventId ?? Guid.NewGuid(),
            AggregateId = aggregateId,
            EventType = eventType,
            EventData = eventData,
            Version = version,
            Timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        });
        await host.Context.SaveChangesAsync();
    }

    private static AggregateStore CreateAggregateStore(EventSourcingDbContext context)
    {
        var eventStore = new EventStore(context);
        var snapshotStore = new SnapshotStore(context);
        return new AggregateStore(
            eventStore,
            snapshotStore,
            new EventCountSnapshotStrategy(),
            context,
            NullLogger<AggregateStore>.Instance);
    }

    private static BankAccount CreateCommittedAccount(string aggregateId, int version)
    {
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version), "版本必须大于 0");

        var account = new BankAccount(aggregateId, "Alice", 0m);
        for (var i = 1; i < version; i++)
        {
            account.Deposit(1m);
        }

        account.MarkEventsAsCommitted();
        return account;
    }

    private sealed class ThrowingSnapshotStore : ISnapshotStore
    {
        public Task SaveSnapshotAsync(ISnapshot snapshot)
        {
            throw new InvalidOperationException("snapshot save failed");
        }

        public Task<T?> GetSnapshotAsync<T>(string aggregateId) where T : class, ISnapshot
        {
            return Task.FromResult<T?>(null);
        }

        public Task<SnapshotMetadata?> GetLatestSnapshotMetadataAsync<T>(string aggregateId) where T : class, ISnapshot
        {
            return Task.FromResult<SnapshotMetadata?>(null);
        }

        public Task<T?> GetSnapshotAsync<T>(string aggregateId, DateTime pointInTime) where T : class, ISnapshot
        {
            return Task.FromResult<T?>(null);
        }
    }

    private sealed class InsertDuplicateEventBeforeSaveInterceptor : SaveChangesInterceptor
    {
        private readonly string _databasePath;
        private readonly string _aggregateId;
        private bool _hasInserted;

        public InsertDuplicateEventBeforeSaveInterceptor(string databasePath, string aggregateId)
        {
            _databasePath = databasePath;
            _aggregateId = aggregateId;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_hasInserted || eventData.Context?.ChangeTracker.Entries<EventStoreEntity>()
                    .All(e => e.Entity.AggregateId != _aggregateId) != false)
            {
                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            _hasInserted = true;
            await using var context = EventSourcingTestHost.CreateContext(_databasePath);
            context.Events.Add(new EventStoreEntity
            {
                Id = Guid.NewGuid(),
                AggregateId = _aggregateId,
                EventType = nameof(AccountCreated),
                EventData = EventTestData.Serialize(new AccountCreated(_aggregateId, "Race", 1m)
                    .WithMetadata(_aggregateId, 1, DateTime.UtcNow)),
                Version = 1,
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
