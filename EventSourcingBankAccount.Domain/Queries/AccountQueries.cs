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
    /// <summary>
    /// UTC 时间点。外部输入必须在 API 边界归一化后再进入查询层。
    /// </summary>
    public DateTime PointInTime { get; set; }
    
    public GetAccountStateAtTime(string accountId, DateTime pointInTime)
    {
        AccountId = accountId;
        PointInTime = pointInTime.Kind switch
        {
            DateTimeKind.Utc => pointInTime,
            DateTimeKind.Local => pointInTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(pointInTime, DateTimeKind.Utc)
        };
    }
    
    // 为序列化器提供无参构造函数
    protected GetAccountStateAtTime() { }
}
