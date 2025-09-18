namespace EventSourcingBankAccount.Api.Models;

/// <summary>
/// 创建账户请求
/// </summary>
public class CreateAccountRequest
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; } = 0;
}

/// <summary>
/// 存款请求
/// </summary>
public class DepositRequest
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 取款请求
/// </summary>
public class WithdrawRequest
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 回溯状态请求
/// </summary>
public class GetStateAtTimeRequest
{
    public DateTime PointInTime { get; set; }
}

/// <summary>
/// API 响应基类
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    
    public static ApiResponse<T> Ok(T data, string message = "操作成功")
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