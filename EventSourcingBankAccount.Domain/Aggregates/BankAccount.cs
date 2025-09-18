using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Domain.Exceptions;

namespace EventSourcingBankAccount.Domain.Aggregates;

/// <summary>
/// 银行账户聚合根
/// </summary>
public class BankAccount : AggregateRoot
{
    public string AccountHolder { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastModifiedAt { get; private set; }
    
    // 为序列化器和基类提供无参构造函数
    private BankAccount() { }
    
    /// <summary>
    /// 创建新的银行账户
    /// </summary>
    public BankAccount(string accountId, string accountHolder, decimal initialBalance = 0)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("账户ID不能为空", nameof(accountId));
        
        if (string.IsNullOrWhiteSpace(accountHolder))
            throw new ArgumentException("账户持有人不能为空", nameof(accountHolder));
        
        if (initialBalance < 0)
            throw new ArgumentException("初始余额不能为负数", nameof(initialBalance));
        
        var @event = new AccountCreated(accountId, accountHolder, initialBalance);
        AddEvent(@event);
        ApplyEvent(@event);
    }
    
    /// <summary>
    /// 存款操作
    /// </summary>
    public void Deposit(decimal amount, string description = "")
    {
        if (amount <= 0)
            throw new InvalidOperationException("存款金额必须大于0");
        
        var newBalance = Balance + amount;
        var @event = new MoneyDeposited(Id, amount, newBalance, description);
        AddEvent(@event);
        ApplyEvent(@event);
    }
    
    /// <summary>
    /// 取款操作
    /// </summary>
    public void Withdraw(decimal amount, string description = "")
    {
        if (amount <= 0)
            throw new InvalidOperationException("取款金额必须大于0");
        
        if (Balance < amount)
            throw new InsufficientFundsException($"余额不足。当前余额: {Balance}, 尝试取款: {amount}");
        
        var newBalance = Balance - amount;
        var @event = new MoneyWithdrawn(Id, amount, newBalance, description);
        AddEvent(@event);
        ApplyEvent(@event);
    }
    
    /// <summary>
    /// 创建快照
    /// </summary>
    public BankAccountSnapshot CreateSnapshot()
    {
        return new BankAccountSnapshot
        {
            AggregateId = Id,
            Version = Version,
            Timestamp = DateTime.UtcNow,
            AccountHolder = AccountHolder,
            Balance = Balance,
            CreatedAt = CreatedAt,
            LastModifiedAt = LastModifiedAt
        };
    }
    
    /// <summary>
    /// 从快照加载
    /// </summary>
    public void LoadFromSnapshot(BankAccountSnapshot snapshot)
    {
        Id = snapshot.AggregateId;
        Version = snapshot.Version;
        AccountHolder = snapshot.AccountHolder;
        Balance = snapshot.Balance;
        CreatedAt = snapshot.CreatedAt;
        LastModifiedAt = snapshot.LastModifiedAt;
    }
    
    protected override void ApplyEvent(DomainEvent @event)
    {
        switch (@event)
        {
            case AccountCreated accountCreated:
                Apply(accountCreated);
                break;
            case MoneyDeposited moneyDeposited:
                Apply(moneyDeposited);
                break;
            case MoneyWithdrawn moneyWithdrawn:
                Apply(moneyWithdrawn);
                break;
        }
        
        LastModifiedAt = @event.Timestamp;
    }
    
    private void Apply(AccountCreated @event)
    {
        Id = @event.AccountId;
        AccountHolder = @event.AccountHolder;
        Balance = @event.InitialBalance;
        CreatedAt = @event.Timestamp;
        LastModifiedAt = @event.Timestamp;
    }
    
    private void Apply(MoneyDeposited @event)
    {
        Balance = @event.NewBalance;
    }
    
    private void Apply(MoneyWithdrawn @event)
    {
        Balance = @event.NewBalance;
    }
}

/// <summary>
/// 银行账户快照
/// </summary>
public class BankAccountSnapshot : ISnapshot
{
    public string AggregateId { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
}