using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Events;

/// <summary>
/// 存款完成事件
/// </summary>
public class MoneyDeposited : DomainEvent
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal NewBalance { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public MoneyDeposited(string accountId, decimal amount, decimal newBalance, string description = "")
    {
        AccountId = accountId;
        Amount = amount;
        NewBalance = newBalance;
        Description = description;
    }
    
    // 为序列化器提供无参构造函数
    protected MoneyDeposited() { }
}