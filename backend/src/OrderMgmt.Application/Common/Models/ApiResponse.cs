namespace OrderMgmt.Application.Common.Models;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(ApiError error) => new() { Success = false, Error = error };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok() => new() { Success = true };
    public new static ApiResponse Fail(ApiError error) => new() { Success = false, Error = error };
}

public class ApiError
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public IDictionary<string, string[]>? Details { get; init; }
    public string? Token { get; init; }
}
