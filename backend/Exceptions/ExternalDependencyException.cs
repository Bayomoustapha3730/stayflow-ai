namespace StayFlow.Api.Exceptions;

public sealed class ExternalDependencyException : Exception
{
    public ExternalDependencyException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
