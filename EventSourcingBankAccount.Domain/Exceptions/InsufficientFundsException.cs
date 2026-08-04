namespace EventSourcingBankAccount.Domain.Exceptions;

/// <summary>
/// 余额不足异常
/// </summary>
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message)
    {
    }
    
    public InsufficientFundsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}