namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// ¿ìÕÕ½Ó¿Ú
/// </summary>
public interface ISnapshot
{
    string AggregateId { get; }
    int Version { get; }
    DateTime Timestamp { get; }
}