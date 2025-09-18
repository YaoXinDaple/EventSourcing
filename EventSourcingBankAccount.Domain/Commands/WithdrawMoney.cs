using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Commands;

/// <summary>
/// 取款命令
/// </summary>
public class WithdrawMoney : DomainCommand
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public WithdrawMoney(string accountId, decimal amount, string description = "")
    {
        AccountId = accountId;
        Amount = amount;
        Description = description;
    }
    
    // 为序列化器提供无参构造函数
    protected WithdrawMoney() { }
}