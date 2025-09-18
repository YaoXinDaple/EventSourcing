namespace EventSourcingBankAccount.Api.Models;

/// <summary>
/// �����˻�����
/// </summary>
public class CreateAccountRequest
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; } = 0;
}

/// <summary>
/// �������
/// </summary>
public class DepositRequest
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// ȡ������
/// </summary>
public class WithdrawRequest
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// ����״̬����
/// </summary>
public class GetStateAtTimeRequest
{
    public DateTime PointInTime { get; set; }
}

/// <summary>
/// API ��Ӧ����
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    
    public static ApiResponse<T> Ok(T data, string message = "�����ɹ�")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }
    
    public static ApiResponse<T> Error(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message
        };
    }
}