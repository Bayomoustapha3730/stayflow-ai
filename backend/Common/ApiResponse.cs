namespace StayFlow.Api.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyCollection<string> Errors { get; init; } = [];
    public string CorrelationId { get; init; } = string.Empty;

    public static ApiResponse<T> Ok(
        T data,
        string message = "Request completed successfully.",
        string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            CorrelationId = ResolveCorrelationId(correlationId)
        };
    }

    public static ApiResponse<T> Fail(
        string message,
        IReadOnlyCollection<string>? errors = null,
        string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? [],
            CorrelationId = ResolveCorrelationId(correlationId)
        };
    }

    private static string ResolveCorrelationId(string? correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("N")
            : correlationId;
    }
}
