using EventSourcingBankAccount.Domain.Events;
using EventSourcingBankAccount.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EventSourcingBankAccount.Infrastructure.Handlers;

/// <summary>
/// 账户创建事件处理器
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
        // 这里可以实现事件的副作用处理，比如：
        // 1. 更新读取模型
        // 2. 发送通知
        // 3. 集成其他系统
        _logger.LogInformation("账户已创建: {AccountId}, 持有人: {AccountHolder}, 初始余额: {InitialBalance}", 
            @event.AccountId, @event.AccountHolder, @event.InitialBalance);

        // 模拟异步处理
        await Task.CompletedTask;
    }
}

/// <summary>
/// 存款完成事件处理器
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
        // 这里可以实现事件的副作用处理，比如：
        // 1. 更新账户余额的读取模型
        // 2. 发送存款成功通知
        // 3. 更新统计数据
        _logger.LogInformation("存款完成: 账户 {AccountId}, 金额: {Amount}, 新余额: {NewBalance}", 
            @event.AccountId, @event.Amount, @event.NewBalance);

        // 模拟异步处理
        await Task.CompletedTask;
    }
}

/// <summary>
/// 取款完成事件处理器
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
        // 这里可以实现事件的副作用处理，比如：
        // 1. 更新账户余额的读取模型
        // 2. 发送取款成功通知
        // 3. 检查余额预警
        _logger.LogInformation("取款完成: 账户 {AccountId}, 金额: {Amount}, 新余额: {NewBalance}", 
            @event.AccountId, @event.Amount, @event.NewBalance);

        // 模拟异步处理
        await Task.CompletedTask;
    }
}