namespace StayFlow.Api.Exceptions;

public sealed class ConflictException : Exception
{
    public ConflictException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
