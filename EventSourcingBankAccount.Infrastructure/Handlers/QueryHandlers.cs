using EventSourcingBankAccount.Domain.Queries;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Domain.Aggregates;

namespace EventSourcingBankAccount.Infrastructure.Handlers;

/// <summary>
/// �˻�����ѯ���
/// </summary>
public class AccountBalanceResult
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// �˻���ʷ״̬��ѯ���
/// </summary>
public class AccountStateResult
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public DateTime StateAtTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// ��ȡ�˻�����ѯ������
/// </summary>
public class GetAccountBalanceHandler : IQueryHandler<GetAccountBalance, AccountBalanceResult>
{
    private readonly IAggregateStore _aggregateStore;

    public GetAccountBalanceHandler(IAggregateStore aggregateStore)
    {
        _aggregateStore = aggregateStore;
    }

    public async Task<AccountBalanceResult> HandleAsync(GetAccountBalance query)
    {
        var account = await _aggregateStore.GetByIdAsync<BankAccount>(query.AccountId);
        
        if (account == null)
        {
            throw new InvalidOperationException($"�˻� {query.AccountId} ������");
        }

        return new AccountBalanceResult
        {
            AccountId = account.Id,
            Balance = account.Balance,
            AccountHolder = account.AccountHolder,
            CreatedAt = account.CreatedAt,
            LastModifiedAt = account.LastModifiedAt,
            Version = account.Version
        };
    }
}

/// <summary>
/// ��ȡ�˻���ʷ״̬��ѯ������
/// </summary>
public class GetAccountStateAtTimeHandler : IQueryHandler<GetAccountStateAtTime, AccountStateResult>
{
    private readonly IAggregateStore _aggregateStore;

    public GetAccountStateAtTimeHandler(IAggregateStore aggregateStore)
    {
        _aggregateStore = aggregateStore;
    }

    public async Task<AccountStateResult> HandleAsync(GetAccountStateAtTime query)
    {
        var account = await _aggregateStore.GetByIdAsync<BankAccount>(query.AccountId, query.PointInTime);
        
        if (account == null)
        {
            throw new InvalidOperationException($"�˻� {query.AccountId} �� {query.PointInTime:yyyy-MM-dd HH:mm:ss} ʱ�����ڻ���δ����");
        }

        return new AccountStateResult
        {
            AccountId = account.Id,
            Balance = account.Balance,
            AccountHolder = account.AccountHolder,
            StateAtTime = query.PointInTime,
            CreatedAt = account.CreatedAt,
            LastModifiedAt = account.LastModifiedAt,
            Version = account.Version
        };
    }
}