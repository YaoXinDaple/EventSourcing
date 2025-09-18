using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Queries;

/// <summary>
/// ��ȡ�˻�����ѯ
/// </summary>
public class GetAccountBalance : DomainCommand
{
    public string AccountId { get; set; } = string.Empty;
    
    public GetAccountBalance(string accountId)
    {
        AccountId = accountId;
    }
    
    // Ϊ���л����ṩ�޲ι��캯��
    protected GetAccountBalance() { }
}

/// <summary>
/// ��ȡ�˻���ʷ״̬��ѯ
/// </summary>
public class GetAccountStateAtTime : DomainCommand
{
    public string AccountId { get; set; } = string.Empty;
    public DateTime PointInTime { get; set; }
    
    public GetAccountStateAtTime(string accountId, DateTime pointInTime)
    {
        AccountId = accountId;
        PointInTime = pointInTime;
    }
    
    // Ϊ���л����ṩ�޲ι��캯��
    protected GetAccountStateAtTime() { }
}