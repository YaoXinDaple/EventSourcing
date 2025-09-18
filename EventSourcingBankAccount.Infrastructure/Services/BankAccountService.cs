using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace EventSourcingBankAccount.Infrastructure.Services;

/// <summary>
/// 银行账户服务接口
/// </summary>
public interface IBankAccountService
{
    Task<BankAccount> CreateAccountAsync(string accountId, string accountHolder, decimal initialBalance = 0);
    Task DepositAsync(string accountId, decimal amount, string description = "");
    Task WithdrawAsync(string accountId, decimal amount, string description = "");
    Task<BankAccount?> GetAccountAsync(string accountId);
    Task<BankAccount?> GetAccountAtTimeAsync(string accountId, DateTime pointInTime);
    Task<decimal> GetBalanceAsync(string accountId);
    Task<bool> AccountExistsAsync(string accountId);
    Task TransferAsync(string fromAccountId, string toAccountId, decimal amount, string description = "");
}

/// <summary>
/// 银行账户服务实现
/// </summary>
public class BankAccountService : IBankAccountService
{
    private readonly IBankAccountRepository _repository;
    private readonly ILogger<BankAccountService> _logger;

    public BankAccountService(IBankAccountRepository repository, ILogger<BankAccountService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BankAccount> CreateAccountAsync(string accountId, string accountHolder, decimal initialBalance = 0)
    {
        _logger.LogInformation("Creating account {AccountId} for {AccountHolder} with initial balance {InitialBalance}",
            accountId, accountHolder, initialBalance);

        // 检查账户是否已存在
        if (await _repository.ExistsAsync(accountId))
        {
            throw new InvalidOperationException($"账户 {accountId} 已存在");
        }

        var account = new BankAccount(accountId, accountHolder, initialBalance);
        await _repository.SaveAsync(account);

        _logger.LogInformation("Account {AccountId} created successfully", accountId);
        return account;
    }

    public async Task DepositAsync(string accountId, decimal amount, string description = "")
    {
        _logger.LogInformation("Processing deposit of {Amount} to account {AccountId}", amount, accountId);

        var account = await GetAccountOrThrowAsync(accountId);
        account.Deposit(amount, description);
        await _repository.SaveAsync(account);

        _logger.LogInformation("Deposit completed for account {AccountId}, new balance: {Balance}",
            accountId, account.Balance);
    }

    public async Task WithdrawAsync(string accountId, decimal amount, string description = "")
    {
        _logger.LogInformation("Processing withdrawal of {Amount} from account {AccountId}", amount, accountId);

        var account = await GetAccountOrThrowAsync(accountId);
        account.Withdraw(amount, description);
        await _repository.SaveAsync(account);

        _logger.LogInformation("Withdrawal completed for account {AccountId}, new balance: {Balance}",
            accountId, account.Balance);
    }

    public async Task<BankAccount?> GetAccountAsync(string accountId)
    {
        return await _repository.GetByIdAsync(accountId);
    }

    public async Task<BankAccount?> GetAccountAtTimeAsync(string accountId, DateTime pointInTime)
    {
        return await _repository.GetByIdAsync(accountId, pointInTime);
    }

    public async Task<decimal> GetBalanceAsync(string accountId)
    {
        var account = await GetAccountOrThrowAsync(accountId);
        return account.Balance;
    }

    public async Task<bool> AccountExistsAsync(string accountId)
    {
        return await _repository.ExistsAsync(accountId);
    }

    public async Task TransferAsync(string fromAccountId, string toAccountId, decimal amount, string description = "")
    {
        _logger.LogInformation("Processing transfer of {Amount} from {FromAccount} to {ToAccount}",
            amount, fromAccountId, toAccountId);

        if (fromAccountId == toAccountId)
        {
            throw new InvalidOperationException("不能向同一账户转账");
        }

        var fromAccount = await GetAccountOrThrowAsync(fromAccountId);
        var toAccount = await GetAccountOrThrowAsync(toAccountId);

        // 执行转账操作
        var transferDescription = string.IsNullOrEmpty(description)
            ? $"转账到 {toAccountId}"
            : $"转账到 {toAccountId}: {description}";

        var receiveDescription = string.IsNullOrEmpty(description)
            ? $"来自 {fromAccountId} 的转账"
            : $"来自 {fromAccountId} 的转账: {description}";

        fromAccount.Withdraw(amount, transferDescription);
        toAccount.Deposit(amount, receiveDescription);

        // 保存两个账户的更改
        await _repository.SaveAsync(fromAccount);
        await _repository.SaveAsync(toAccount);

        _logger.LogInformation("Transfer completed: {Amount} from {FromAccount} to {ToAccount}",
            amount, fromAccountId, toAccountId);
    }

    private async Task<BankAccount> GetAccountOrThrowAsync(string accountId)
    {
        var account = await _repository.GetByIdAsync(accountId);
        if (account == null)
        {
            throw new InvalidOperationException($"账户 {accountId} 不存在");
        }
        return account;
    }
}
