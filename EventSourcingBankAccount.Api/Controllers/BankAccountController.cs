using Microsoft.AspNetCore.Mvc;
using EventSourcingBankAccount.Api.Models;
using EventSourcingBankAccount.Domain.Commands;
using EventSourcingBankAccount.Domain.Queries;
using EventSourcingBankAccount.Domain.Interfaces;
using EventSourcingBankAccount.Domain.Aggregates;
using EventSourcingBankAccount.Infrastructure.Handlers;
using EventSourcingBankAccount.Domain.Exceptions;
using EventSourcingBankAccount.Infrastructure.Repositories;

namespace EventSourcingBankAccount.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BankAccountController : ControllerBase
{
    private readonly ICommandHandler<CreateAccount> _createAccountHandler;
    private readonly ICommandHandler<DepositMoney> _depositHandler;
    private readonly ICommandHandler<WithdrawMoney> _withdrawHandler;
    private readonly IQueryHandler<GetAccountBalance, AccountBalanceResult> _balanceQueryHandler;
    private readonly IQueryHandler<GetAccountStateAtTime, AccountStateResult> _stateQueryHandler;
    private readonly ILogger<BankAccountController> _logger;

    public BankAccountController(
        ICommandHandler<CreateAccount> createAccountHandler,
        ICommandHandler<DepositMoney> depositHandler,
        ICommandHandler<WithdrawMoney> withdrawHandler,
        IQueryHandler<GetAccountBalance, AccountBalanceResult> balanceQueryHandler,
        IQueryHandler<GetAccountStateAtTime, AccountStateResult> stateQueryHandler,
        ILogger<BankAccountController> logger)
    {        _createAccountHandler = createAccountHandler;
        _depositHandler = depositHandler;
        _withdrawHandler = withdrawHandler;
        _balanceQueryHandler = balanceQueryHandler;
        _stateQueryHandler = stateQueryHandler;
        _logger = logger;
    }

    /// <summary>
    /// 创建银行账户
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.AccountId))
            {
                return BadRequest(ApiResponse<object>.Error("账户ID不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.AccountHolder))
            {
                return BadRequest(ApiResponse<object>.Error("账户持有人不能为空"));
            }

            if (request.InitialBalance < 0)
            {
                return BadRequest(ApiResponse<object>.Error("初始余额不能为负数"));
            }

            // 使用命令处理器创建账户
            var command = new CreateAccount(request.AccountId, request.AccountHolder, request.InitialBalance);
            await _createAccountHandler.HandleAsync(command);

            _logger.LogInformation("账户创建成功: {AccountId}", request.AccountId);

            return Ok(ApiResponse<object>.Ok(new { AccountId = request.AccountId }, "账户创建成功"));
        }
        catch (AccountAlreadyExistsException ex)
        {
            _logger.LogWarning(ex, "尝试创建已存在的账户: {AccountId}", request.AccountId);
            return Conflict(ApiResponse<object>.Error(ex.Message));
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "创建账户并发冲突: {AccountId}", request.AccountId);
            return Conflict(ApiResponse<object>.Error("账户数据发生并发冲突，请刷新后重试"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建账户时发生错误: {AccountId}", request.AccountId);
            return StatusCode(500, ApiResponse<object>.Error($"创建账户失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 存款操作
    /// </summary>
    [HttpPost("{accountId}/deposit")]
    public async Task<ActionResult<ApiResponse<object>>> Deposit(string accountId, [FromBody] DepositRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(ApiResponse<object>.Error("存款金额必须大于0"));
            }

            var command = new DepositMoney(accountId, request.Amount, request.Description);
            await _depositHandler.HandleAsync(command);

            _logger.LogInformation("存款成功: 账户 {AccountId}, 金额 {Amount}", accountId, request.Amount);

            return Ok(ApiResponse<object>.Ok(new { AccountId = accountId, Amount = request.Amount }, "存款成功"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "存款操作失败: 账户 {AccountId}", accountId);
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "存款并发冲突: 账户 {AccountId}", accountId);
            return Conflict(ApiResponse<object>.Error("账户数据发生并发冲突，请刷新后重试"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "存款时发生错误: 账户 {AccountId}", accountId);
            return StatusCode(500, ApiResponse<object>.Error($"存款失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 取款操作
    /// </summary>
    [HttpPost("{accountId}/withdraw")]
    public async Task<ActionResult<ApiResponse<object>>> Withdraw(string accountId, [FromBody] WithdrawRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(ApiResponse<object>.Error("取款金额必须大于0"));
            }

            var command = new WithdrawMoney(accountId, request.Amount, request.Description);
            await _withdrawHandler.HandleAsync(command);

            _logger.LogInformation("取款成功: 账户 {AccountId}, 金额 {Amount}", accountId, request.Amount);

            return Ok(ApiResponse<object>.Ok(new { AccountId = accountId, Amount = request.Amount }, "取款成功"));
        }
        catch (InsufficientFundsException ex)
        {
            _logger.LogWarning(ex, "取款失败 - 余额不足: 账户 {AccountId}", accountId);
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "取款操作失败: 账户 {AccountId}", accountId);
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "取款并发冲突: 账户 {AccountId}", accountId);
            return Conflict(ApiResponse<object>.Error("账户数据发生并发冲突，请刷新后重试"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取款时发生错误: 账户 {AccountId}", accountId);
            return StatusCode(500, ApiResponse<object>.Error($"取款失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 查询账户余额
    /// </summary>
    [HttpGet("{accountId}/balance")]
    public async Task<ActionResult<ApiResponse<AccountBalanceResult>>> GetBalance(string accountId)
    {
        try
        {
            var query = new GetAccountBalance(accountId);
            var result = await _balanceQueryHandler.HandleAsync(query);

            return Ok(ApiResponse<AccountBalanceResult>.Ok(result, "查询成功"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "账户余额查询失败: {AccountId}", accountId);
            return NotFound(ApiResponse<AccountBalanceResult>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询账户余额时发生错误: {AccountId}", accountId);
            return StatusCode(500, ApiResponse<AccountBalanceResult>.Error($"查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 回溯账户状态到指定时间点
    /// </summary>
    /// <param name="accountId">账户ID。</param>
    /// <param name="pointInTime">
    /// 回溯时间点。建议传入带明确时区偏移的 ISO 8601 时间，例如 2026-08-04T10:00:00+08:00 或
    /// 2026-08-04T02:00:00Z；两者表示同一瞬间。服务端会统一转换为 UTC 后查询。
    /// 如果传入不带偏移的时间（例如 2026-08-04T10:00:00），将保留 ASP.NET Core 的默认绑定语义，
    /// 按服务器本地时区解释后再转换为 UTC。
    /// </param>
    [HttpGet("{accountId}/state")]
    public async Task<ActionResult<ApiResponse<AccountStateResult>>> GetStateAtTime(
        string accountId, 
        [FromQuery] DateTimeOffset pointInTime)
    {
        try
        {
            if (pointInTime == default)
            {
                return BadRequest(ApiResponse<AccountStateResult>.Error("必须指定时间点"));
            }

            var utcPointInTime = pointInTime.UtcDateTime;
            var query = new GetAccountStateAtTime(accountId, utcPointInTime);
            var result = await _stateQueryHandler.HandleAsync(query);

            return Ok(ApiResponse<AccountStateResult>.Ok(result, "状态回溯成功"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "账户状态回溯失败: {AccountId}, 时间点: {PointInTime}", accountId, pointInTime);
            return NotFound(ApiResponse<AccountStateResult>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "状态回溯时发生错误: {AccountId}, 时间点: {PointInTime}", accountId, pointInTime);
            return StatusCode(500, ApiResponse<AccountStateResult>.Error($"状态回溯失败: {ex.Message}"));
        }
    }
}
