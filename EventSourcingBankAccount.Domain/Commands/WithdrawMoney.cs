using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Commands;

/// <summary>
/// ȡ������
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
    
    // Ϊ���л����ṩ�޲ι��캯��
    protected WithdrawMoney() { }
}