namespace EventSourcingBankAccount.Domain.Exceptions;

/// <summary>
/// 事件反序列化异常
/// </summary>
public class EventDeserializationException : Exception
{
    public string AggregateId { get; }
    public int Version { get; }
    public string EventType { get; }

    public EventDeserializationException(string aggregateId, int version, string eventType, string message)
        : base(CreateMessage(aggregateId, version, eventType, message))
    {
        AggregateId = aggregateId;
        Version = version;
        EventType = eventType;
    }

    public EventDeserializationException(string aggregateId, int version, string eventType, string message, Exception innerException)
        : base(CreateMessage(aggregateId, version, eventType, message), innerException)
    {
        AggregateId = aggregateId;
        Version = version;
        EventType = eventType;
    }

    private static string CreateMessage(string aggregateId, int version, string eventType, string message)
    {
        return $"事件反序列化失败。AggregateId: {aggregateId}, Version: {version}, EventType: {eventType}. {message}";
    }
}
