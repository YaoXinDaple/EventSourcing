using EventSourcingBankAccount.Domain.Core;

namespace EventSourcingBankAccount.Domain.Queries;

/// <summary>
/// 获取账户余额查询
/// </summary>
public class GetAccountBalance : DomainCommand
{
    public string AccountId { get; set; } = string.Empty;
    
    public GetAccountBalance(string accountId)
    {
        AccountId = accountId;
    }
    
    // 为序列化器提供无参构造函数
    protected GetAccountBalance() { }
}

/// <summary>
/// 获取账户历史状态查询
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
    
    // 为序列化器提供无参构造函数
    protected GetAccountStateAtTime() { }
}