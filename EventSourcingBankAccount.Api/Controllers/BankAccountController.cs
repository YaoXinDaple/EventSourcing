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
    /// ���������˻�
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.AccountId))
            {
                return BadRequest(ApiResponse<object>.Error("�˻�ID����Ϊ��"));
            }

            if (string.IsNullOrWhiteSpace(request.AccountHolder))
            {
                return BadRequest(ApiResponse<object>.Error("�˻������˲���Ϊ��"));
            }

            if (request.InitialBalance < 0)
            {
                return BadRequest(ApiResponse<object>.Error("��ʼ����Ϊ����"));
            }

            // ʹ��������������˻�
            var command = new CreateAccount(request.AccountId, request.AccountHolder, request.InitialBalance);
            await _createAccountHandler.HandleAsync(command);

            _logger.LogInformation("�˻������ɹ�: {AccountId}", request.AccountId);

            return Ok(ApiResponse<object>.Ok(new { AccountId = request.AccountId }, "�˻������ɹ�"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("�Ѵ���"))
        {
            _logger.LogWarning("���Դ����Ѵ��ڵ��˻�: {AccountId}", request.AccountId);
            return Conflict(ApiResponse<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "�����˻�ʱ��������: {AccountId}", request.AccountId);
            return StatusCode(500, ApiResponse<object>.Error($"�����˻�ʧ��: {ex.Message}"));
        }
    }

    /// <summary>
    /// ������
    /// </summary>
    [HttpPost("{accountId}/deposit")]
    public async Task<ActionResult<ApiResponse<object>>> Deposit(string accountId, [FromBody] DepositRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(ApiResponse<object>.Error("�����������0"));
            }

            var command = new DepositMoney(accountId, request.Amount, request.Description);
            await _depositHandler.HandleAsync(command);

            _logger.LogInformation("���ɹ�: �˻� {AccountId}, ��� {Amount}", accountId, request.Amount);

            return Ok(ApiResponse<object>.Ok(new { AccountId = accountId, Amount = request.Amount }, "���ɹ�"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "������ʧ��: �˻� {AccountId}", accountId);
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "���ʱ��������: �˻� {AccountId}", accountId);
            return StatusCode(500, ApiResponse<object>.Error($"���ʧ��: {ex.Message}"));
        }
    }

    /// <summary>
    /// ȡ�����
    /// </summary>
    [HttpPost("{accountId}/withdraw")]
    public async Task<ActionResult<ApiResponse<object>>> Withdraw(string accountId, [FromBody] WithdrawRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(ApiResponse<object>.Error("ȡ����������0"));
            }

            var command = new WithdrawMoney(accountId, request.Amount, request.Description);
            await _withdrawHandler.HandleAsync(command);

            _logger.LogInformation("ȡ��ɹ�: �˻� {AccountId}, ��� {Amount}", accountId, request.Amount);

            return Ok(ApiResponse<object>.Ok(new { AccountId = accountId, Amount = request.Amount }, "ȡ��ɹ�"));
        }
        catch (InsufficientFundsException ex)
        {
            _logger.LogWarning(ex, "ȡ��ʧ�� - ����: �˻� {AccountId}", accountId);
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ȡ�����ʧ��: �˻� {AccountId}", accountId);
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ȡ��ʱ��������: �˻� {AccountId}", accountId);
            return StatusCode(500, ApiResponse<object>.Error($"ȡ��ʧ��: {ex.Message}"));
        }
    }

    /// <summary>
    /// ��ѯ�˻����
    /// </summary>
    [HttpGet("{accountId}/balance")]
    public async Task<ActionResult<ApiResponse<AccountBalanceResult>>> GetBalance(string accountId)
    {
        try
        {
            var query = new GetAccountBalance(accountId);
            var result = await _balanceQueryHandler.HandleAsync(query);

            return Ok(ApiResponse<AccountBalanceResult>.Ok(result, "��ѯ�ɹ�"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "�˻�����ѯʧ��: {AccountId}", accountId);
            return NotFound(ApiResponse<AccountBalanceResult>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "��ѯ�˻����ʱ��������: {AccountId}", accountId);
            return StatusCode(500, ApiResponse<AccountBalanceResult>.Error($"��ѯʧ��: {ex.Message}"));
        }
    }

    /// <summary>
    /// �����˻�״̬��ָ��ʱ���
    /// </summary>
    [HttpGet("{accountId}/state")]
    public async Task<ActionResult<ApiResponse<AccountStateResult>>> GetStateAtTime(
        string accountId, 
        [FromQuery] DateTime pointInTime)
    {
        try
        {
            if (pointInTime == default)
            {
                return BadRequest(ApiResponse<AccountStateResult>.Error("����ָ��ʱ���"));
            }

            var query = new GetAccountStateAtTime(accountId, pointInTime);
            var result = await _stateQueryHandler.HandleAsync(query);

            return Ok(ApiResponse<AccountStateResult>.Ok(result, "״̬���ݳɹ�"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "�˻�״̬����ʧ��: {AccountId}, ʱ���: {PointInTime}", accountId, pointInTime);
            return NotFound(ApiResponse<AccountStateResult>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "״̬����ʱ��������: {AccountId}, ʱ���: {PointInTime}", accountId, pointInTime);
            return StatusCode(500, ApiResponse<AccountStateResult>.Error($"״̬����ʧ��: {ex.Message}"));
        }
    }
}