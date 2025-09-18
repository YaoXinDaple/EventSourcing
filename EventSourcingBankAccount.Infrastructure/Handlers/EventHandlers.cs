using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EventSourcingBankAccount.Infrastructure.Handlers;

/// <summary>
/// �˻������¼�������
/// </summary>
public class AccountCreatedHandler : IEventHandler<AccountCreated>
{
    private readonly ILogger<AccountCreatedHandler> _logger;

    public AccountCreatedHandler(ILogger<AccountCreatedHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(AccountCreated @event)
    {
        // �������ʵ���¼��ĸ����ô������磺
        // 1. ���¶�ȡģ��
        // 2. ����֪ͨ
        // 3. ��������ϵͳ
        _logger.LogInformation("�˻��Ѵ���: {AccountId}, ������: {AccountHolder}, ��ʼ���: {InitialBalance}", 
            @event.AccountId, @event.AccountHolder, @event.InitialBalance);

        // ģ���첽����
        await Task.CompletedTask;
    }
}

/// <summary>
/// �������¼�������
/// </summary>
public class MoneyDepositedHandler : IEventHandler<MoneyDeposited>
{
    private readonly ILogger<MoneyDepositedHandler> _logger;

    public MoneyDepositedHandler(ILogger<MoneyDepositedHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(MoneyDeposited @event)
    {
        // �������ʵ���¼��ĸ����ô������磺
        // 1. �����˻����Ķ�ȡģ��
        // 2. ���ʹ��ɹ�֪ͨ
        // 3. ����ͳ������
        _logger.LogInformation("������: �˻� {AccountId}, ���: {Amount}, �����: {NewBalance}", 
            @event.AccountId, @event.Amount, @event.NewBalance);

        // ģ���첽����
        await Task.CompletedTask;
    }
}

/// <summary>
/// ȡ������¼�������
/// </summary>
public class MoneyWithdrawnHandler : IEventHandler<MoneyWithdrawn>
{
    private readonly ILogger<MoneyWithdrawnHandler> _logger;

    public MoneyWithdrawnHandler(ILogger<MoneyWithdrawnHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(MoneyWithdrawn @event)
    {
        // �������ʵ���¼��ĸ����ô������磺
        // 1. �����˻����Ķ�ȡģ��
        // 2. ����ȡ��ɹ�֪ͨ
        // 3. ������Ԥ��
        _logger.LogInformation("ȡ�����: �˻� {AccountId}, ���: {Amount}, �����: {NewBalance}", 
            @event.AccountId, @event.Amount, @event.NewBalance);

        // ģ���첽����
        await Task.CompletedTask;
    }
}