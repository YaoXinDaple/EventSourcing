namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// 快照接口
/// </summary>
public interface ISnapshot
{
    string AggregateId { get; }
    int Version { get; }
    DateTime Timestamp { get; }
}