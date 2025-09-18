using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Events;

/// <summary>
/// ȡ������¼�
/// </summary>
public class MoneyWithdrawn : DomainEvent
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal NewBalance { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public MoneyWithdrawn(string accountId, decimal amount, decimal newBalance, string description = "")
    {
        AccountId = accountId;
        Amount = amount;
        NewBalance = newBalance;
        Description = description;
    }
    
    // Ϊ���л����ṩ�޲ι��캯��
    protected MoneyWithdrawn() { }
}