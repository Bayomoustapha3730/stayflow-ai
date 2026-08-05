namespace StayFlow.Api.Exceptions;

public sealed class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
