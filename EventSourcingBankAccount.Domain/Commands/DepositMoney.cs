using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Commands;

/// <summary>
/// 存款命令
/// </summary>
public class DepositMoney : DomainCommand
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public DepositMoney(string accountId, decimal amount, string description = "")
    {
        AccountId = accountId;
        Amount = amount;
        Description = description;
    }
    
    // 为序列化器提供无参构造函数
    protected DepositMoney() { }
}