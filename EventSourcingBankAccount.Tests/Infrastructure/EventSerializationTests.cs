using System.Text.Json;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Tests.TestInfrastructure;

namespace EventSourcingBankAccount.Tests.Infrastructure;

public class EventSerializationTests
{
    [Theory]
    [MemberData(nameof(EventCases))]
    public async Task StoredEvents_ShouldRoundTripAllMetadataAndBusinessFields(DomainEvent source)
    {
        // 回归：事件序列化曾丢失 EventId 和 Timestamp，破坏审计和时间点回溯。
        await using var host = await EventSourcingTestHost.CreateAsync();

        await host.EventStore.SaveEventsAsync(source.AggregateId, [source], expectedVersion: 0);
        var loaded = (await host.EventStore.GetEventsAsync(source.AggregateId)).Single();

        Assert.Equal(source.EventId, loaded.EventId);
        Assert.Equal(source.Timestamp, loaded.Timestamp);
        Assert.Equal(DateTimeKind.Utc, loaded.Timestamp.Kind);
        Assert.Equal(source.Version, loaded.Version);
        Assert.Equal(source.AggregateId, loaded.AggregateId);
        Assert.Equal(source.EventType, loaded.EventType);

        switch (source, loaded)
        {
            case (AccountCreated expected, AccountCreated actual):
                Assert.Equal(expected.AccountId, actual.AccountId);
                Assert.Equal(expected.AccountHolder, actual.AccountHolder);
                Assert.Equal(expected.InitialBalance, actual.InitialBalance);
                break;
            case (MoneyDeposited expected, MoneyDeposited actual):
                Assert.Equal(expected.AccountId, actual.AccountId);
                Assert.Equal(expected.Amount, actual.Amount);
                Assert.Equal(expected.NewBalance, actual.NewBalance);
                Assert.Equal(expected.Description, actual.Description);
                break;
            case (MoneyWithdrawn expected, MoneyWithdrawn actual):
                Assert.Equal(expected.AccountId, actual.AccountId);
                Assert.Equal(expected.Amount, actual.Amount);
                Assert.Equal(expected.NewBalance, actual.NewBalance);
                Assert.Equal(expected.Description, actual.Description);
                break;
            default:
                throw new JsonException("Unexpected test event type.");
        }
    }

    public static TheoryData<DomainEvent> EventCases()
    {
        var aggregateId = "acct-serialize";
        return
        [
            new AccountCreated(aggregateId, "Alice", 100m)
                .WithMetadata(aggregateId, 1, new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc), Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new MoneyDeposited(aggregateId, 25m, 125m, "payroll")
                .WithMetadata(aggregateId, 2, new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc), Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new MoneyWithdrawn(aggregateId, 10m, 115m, "atm")
                .WithMetadata(aggregateId, 3, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), Guid.Parse("33333333-3333-3333-3333-333333333333"))
        ];
    }
}
