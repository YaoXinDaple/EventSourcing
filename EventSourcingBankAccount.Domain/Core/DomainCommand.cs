namespace EventSourcingBankAccount.Domain.Core;

/// <summary>
/// ÁìÓòÃüÁî»ùÀà
/// </summary>
public abstract class DomainCommand
{
    public Guid CommandId { get; protected set; } = Guid.NewGuid();
    public DateTime Timestamp { get; protected set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
}