namespace EventSourcingBankAccount.Domain.Exceptions;

/// <summary>
/// 聚合并发冲突异常。
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string aggregateId, int expectedVersion, int actualVersion)
        : base(CreateMessage(aggregateId, expectedVersion, actualVersion))
    {
        AggregateId = aggregateId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public ConcurrencyException(string aggregateId, int expectedVersion, int actualVersion, Exception innerException)
        : base(CreateMessage(aggregateId, expectedVersion, actualVersion), innerException)
    {
        AggregateId = aggregateId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public string AggregateId { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    private static string CreateMessage(string aggregateId, int expectedVersion, int actualVersion)
    {
        return $"并发冲突。AggregateId: {aggregateId}, ExpectedVersion: {expectedVersion}, ActualVersion: {actualVersion}";
    }
}
