namespace StayFlow.Api.Services.Payments;

public sealed class MpesaCredentialResolution
{
    public bool Success { get; init; }
    public string? ConsumerKey { get; init; }
    public string? ConsumerSecret { get; init; }
    public string? PassKey { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureSummary { get; init; }
}

public interface IMpesaCredentialResolver
{
    /// <summary>Resolves Safaricom Daraja credentials from environment variables. Never persists secrets.</summary>
    Task<MpesaCredentialResolution> ResolveAsync(CancellationToken cancellationToken);
}
