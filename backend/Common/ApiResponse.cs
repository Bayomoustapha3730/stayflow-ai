namespace StayFlow.Api.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyCollection<string> Errors { get; init; } = [];

    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(string message, IReadOnlyCollection<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? []
        };
    }
}
