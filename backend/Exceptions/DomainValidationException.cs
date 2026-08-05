namespace StayFlow.Api.Exceptions;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
