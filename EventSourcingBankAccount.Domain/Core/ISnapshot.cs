namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// ���սӿ�
/// </summary>
public interface ISnapshot
{
    string AggregateId { get; }
    int Version { get; }
    DateTime Timestamp { get; }
}