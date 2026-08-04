using System.Reflection;
using System.Text.Json;
using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Tests.TestInfrastructure;

internal static class EventTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(DomainEvent @event)
    {
        return JsonSerializer.Serialize((object)@event, @event.GetType(), JsonOptions);
    }

    public static T WithMetadata<T>(this T @event, string aggregateId, int version, DateTime timestamp, Guid? eventId = null)
        where T : DomainEvent
    {
        @event.AggregateId = aggregateId;
        @event.Version = version;
        SetProtectedProperty(@event, nameof(DomainEvent.Timestamp), DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

        if (eventId.HasValue)
        {
            SetProtectedProperty(@event, nameof(DomainEvent.EventId), eventId.Value);
        }

        return @event;
    }

    private static void SetProtectedProperty<T>(DomainEvent @event, string propertyName, T value)
    {
        var property = typeof(DomainEvent).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        property!.SetValue(@event, value);
    }
}
