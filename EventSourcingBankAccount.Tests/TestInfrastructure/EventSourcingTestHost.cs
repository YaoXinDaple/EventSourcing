using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventSourcingBankAccount.Tests.TestInfrastructure;

internal sealed class EventSourcingTestHost : IAsyncDisposable
{
    private readonly string _databasePath;

    private EventSourcingTestHost(string databasePath, EventSourcingDbContext context)
    {
        _databasePath = databasePath;
        Context = context;
        EventStore = new EventStore(Context);
        SnapshotStore = new SnapshotStore(Context);
        AggregateStore = new AggregateStore(
            EventStore,
            SnapshotStore,
            new EventCountSnapshotStrategy(),
            Context,
            NullLogger<AggregateStore>.Instance);
    }

    public EventSourcingDbContext Context { get; }
    public EventStore EventStore { get; }
    public SnapshotStore SnapshotStore { get; }
    public AggregateStore AggregateStore { get; }
    public string DatabasePath => _databasePath;

    public static async Task<EventSourcingTestHost> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"eventsourcing-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<EventSourcingDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var context = new EventSourcingDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new EventSourcingTestHost(databasePath, context);
    }

    public static EventSourcingDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<EventSourcingDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new EventSourcingDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        SqliteConnection.ClearAllPools();

        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
