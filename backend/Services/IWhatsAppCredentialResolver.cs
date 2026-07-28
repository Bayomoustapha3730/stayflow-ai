using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCredentialResolution
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? AppSecret { get; init; }
    public string? WebhookVerifyToken { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureSummary { get; init; }
}

public interface IWhatsAppCredentialResolver
{
    Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken);
}
