using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Events;

/// <summary>
/// 账户创建事件
/// </summary>
public class AccountCreated : DomainEvent
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    
    public AccountCreated(string accountId, string accountHolder, decimal initialBalance = 0)
    {
        AccountId = accountId;
        AccountHolder = accountHolder;
        InitialBalance = initialBalance;
    }
    
    // 为序列化器提供无参构造函数
    protected AccountCreated() { }
}