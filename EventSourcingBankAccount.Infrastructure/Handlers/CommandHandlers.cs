using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Commands;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventSourcingBankAccount.Infrastructure.Handlers;

/// <summary>
/// 创建账户命令处理器
/// </summary>
public class CreateAccountHandler : ICommandHandler<CreateAccount>
{
    private readonly IAggregateStore _aggregateStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly EventSourcingDbContext _context;

    public CreateAccountHandler(IAggregateStore aggregateStore,ISnapshotStore snapshotStore, EventSourcingDbContext context)
    {
        _aggregateStore = aggregateStore;
        _snapshotStore = snapshotStore;
        _context = context;
    }

    public async Task HandleAsync(CreateAccount command)
    {
        // 检查账户是否已存在
        var existingAccount = await _aggregateStore.GetByIdAsync<BankAccount>(command.AccountId);
        if (existingAccount != null)
        {
            throw new InvalidOperationException($"账户 {command.AccountId} 已存在");
        }

        // 创建新账户聚合
        var account = new BankAccount(command.AccountId, command.AccountHolder, command.InitialBalance);
        await _snapshotStore.SaveSnapshotAsync(account.CreateSnapshot());

        // 保存聚合
        await _aggregateStore.SaveAsync(account);
    }
}

/// <summary>
/// 存款命令处理器
/// </summary>
public class DepositMoneyHandler : ICommandHandler<DepositMoney>
{
    private readonly IAggregateStore _aggregateStore;

    public DepositMoneyHandler(IAggregateStore aggregateStore)
    {
        _aggregateStore = aggregateStore;
    }

    public async Task HandleAsync(DepositMoney command)
    {
        // 获取现有的银行账户聚合
        var account = await _aggregateStore.GetByIdAsync<BankAccount>(command.AccountId);
        if (account == null)
        {
            // 账户不存在，抛出异常而不是自动创建账户
            throw new InvalidOperationException($"账户 {command.AccountId} 不存在。请先创建账户。");
        }

        // 执行存款操作
        account.Deposit(command.Amount, command.Description);

        // 保存聚合
        await _aggregateStore.SaveAsync(account);
    }
}

/// <summary>
/// 取款命令处理器
/// </summary>
public class WithdrawMoneyHandler : ICommandHandler<WithdrawMoney>
{
    private readonly IAggregateStore _aggregateStore;

    public WithdrawMoneyHandler(IAggregateStore aggregateStore)
    {
        _aggregateStore = aggregateStore;
    }

    public async Task HandleAsync(WithdrawMoney command)
    {
        // 获取现有的银行账户聚合
        var account = await _aggregateStore.GetByIdAsync<BankAccount>(command.AccountId);
        if (account == null)
        {
            throw new InvalidOperationException($"账户 {command.AccountId} 不存在");
        }

        // 执行取款操作
        account.Withdraw(command.Amount, command.Description);

        // 保存聚合
        await _aggregateStore.SaveAsync(account);
    }
}