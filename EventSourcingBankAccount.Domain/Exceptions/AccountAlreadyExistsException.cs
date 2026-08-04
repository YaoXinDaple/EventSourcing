namespace EventSourcingBankAccount.Domain.Exceptions;

/// <summary>
/// 账户已存在异常。
/// </summary>
public class AccountAlreadyExistsException : Exception
{
    public AccountAlreadyExistsException(string accountId)
        : base($"账户 {accountId} 已存在")
    {
        AccountId = accountId;
    }

    public string AccountId { get; }
}
