namespace EventSourcingBankAccount.Domain.Exceptions;

/// <summary>
/// 快照反序列化异常
/// </summary>
public class SnapshotDeserializationException : Exception
{
    public string AggregateId { get; }
    public int Version { get; }
    public string SnapshotType { get; }

    public SnapshotDeserializationException(string aggregateId, int version, string snapshotType, string message)
        : base(CreateMessage(aggregateId, version, snapshotType, message))
    {
        AggregateId = aggregateId;
        Version = version;
        SnapshotType = snapshotType;
    }

    public SnapshotDeserializationException(string aggregateId, int version, string snapshotType, string message, Exception innerException)
        : base(CreateMessage(aggregateId, version, snapshotType, message), innerException)
    {
        AggregateId = aggregateId;
        Version = version;
        SnapshotType = snapshotType;
    }

    private static string CreateMessage(string aggregateId, int version, string snapshotType, string message)
    {
        return $"快照反序列化失败。AggregateId: {aggregateId}, Version: {version}, SnapshotType: {snapshotType}. {message}";
    }
}
