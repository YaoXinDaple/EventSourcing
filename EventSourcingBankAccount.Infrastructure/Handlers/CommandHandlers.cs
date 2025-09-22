using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Domain.Commands;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Infrastructure.Data;
using EventSourcingBankAccount.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventSourcingBankAccount.Infrastructure.Handlers;

/// <summary>
/// �����˻��������
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
        // ����˻��Ƿ��Ѵ���
        var existingAccount = await _aggregateStore.GetByIdAsync<BankAccount>(command.AccountId);
        if (existingAccount != null)
        {
            throw new InvalidOperationException($"�˻� {command.AccountId} �Ѵ���");
        }

        // �������˻��ۺ�
        var account = new BankAccount(command.AccountId, command.AccountHolder, command.InitialBalance);
        await _snapshotStore.SaveSnapshotAsync(account.CreateSnapshot());

        // ����ۺ�
        await _aggregateStore.SaveAsync(account);
    }
}

/// <summary>
/// ����������
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
        // ��ȡ���е������˻��ۺ�
        var account = await _aggregateStore.GetByIdAsync<BankAccount>(command.AccountId);
        if (account == null)
        {
            // �˻������ڣ��׳��쳣�������Զ������˻�
            throw new InvalidOperationException($"�˻� {command.AccountId} �����ڡ����ȴ����˻���");
        }

        // ִ�д�����
        account.Deposit(command.Amount, command.Description);

        // ����ۺ�
        await _aggregateStore.SaveAsync(account);
    }
}

/// <summary>
/// ȡ���������
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
        // ��ȡ���е������˻��ۺ�
        var account = await _aggregateStore.GetByIdAsync<BankAccount>(command.AccountId);
        if (account == null)
        {
            throw new InvalidOperationException($"�˻� {command.AccountId} ������");
        }

        // ִ��ȡ�����
        account.Withdraw(command.Amount, command.Description);

        // ����ۺ�
        await _aggregateStore.SaveAsync(account);
    }
}