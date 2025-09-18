using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Commands;

/// <summary>
/// 创建账户命令
/// </summary>
public class CreateAccount : DomainCommand
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    
    public CreateAccount(string accountId, string accountHolder, decimal initialBalance = 0)
    {
        AccountId = accountId;
        AccountHolder = accountHolder;
        InitialBalance = initialBalance;
    }
    
    // 为反序列化提供无参构造函数
    protected CreateAccount() { }
}