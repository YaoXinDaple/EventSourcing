using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Commands;

/// <summary>
/// �����˻�����
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
    
    // Ϊ�����л��ṩ�޲ι��캯��
    protected CreateAccount() { }
}