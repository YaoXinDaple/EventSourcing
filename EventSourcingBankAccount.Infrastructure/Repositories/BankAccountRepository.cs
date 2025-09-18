using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Core;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EventSourcingBankAccount.Infrastructure.Repositories;

/// <summary>
/// 银行账户仓储接口
/// </summary>
public interface IBankAccountRepository
{
    Task<BankAccount?> GetByIdAsync(string accountId);
    Task<BankAccount?> GetByIdAsync(string accountId, DateTime pointInTime);
    Task SaveAsync(BankAccount account);
    Task<bool> ExistsAsync(string accountId);
    Task<IEnumerable<string>> GetAllAccountIdsAsync();
}

/// <summary>
/// 银行账户仓储实现
/// </summary>
public class BankAccountRepository : IBankAccountRepository
{
    private readonly IAggregateStore _aggregateStore;
    private readonly IEventStore _eventStore;

    public BankAccountRepository(IAggregateStore aggregateStore, IEventStore eventStore)
    {
        _aggregateStore = aggregateStore;
        _eventStore = eventStore;
    }

    public async Task<BankAccount?> GetByIdAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("账户ID不能为空", nameof(accountId));

        return await _aggregateStore.GetByIdAsync<BankAccount>(accountId);
    }

    public async Task<BankAccount?> GetByIdAsync(string accountId, DateTime pointInTime)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("账户ID不能为空", nameof(accountId));

        return await _aggregateStore.GetByIdAsync<BankAccount>(accountId, pointInTime);
    }

    public async Task SaveAsync(BankAccount account)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        await _aggregateStore.SaveAsync(account);
    }

    public async Task<bool> ExistsAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return false;

        var account = await GetByIdAsync(accountId);
        return account != null;
    }

    public async Task<IEnumerable<string>> GetAllAccountIdsAsync()
    {
        // 注意：这个方法在事件溯源中需要扫描所有事件，可能性能较差
        // 在生产环境中，建议维护一个单独的读模型来存储账户列表
        var events = await _eventStore.GetAllEventsAsync();
        return events
            .Where(e => e.EventType == "AccountCreated")
            .Select(e => e.AggregateId)
            .Distinct()
            .ToList();
    }
}
