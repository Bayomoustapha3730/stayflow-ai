namespace StayFlow.Api.Services;

public sealed class WhatsAppWebhookSignatureValidationResult
{
    public bool IsValid { get; init; }
    public string FailureReason { get; init; } = string.Empty;
}

public interface IWhatsAppWebhookSignatureVerifier
{
    Task<bool> IsWebhookVerificationTokenValidAsync(string? providedToken, CancellationToken cancellationToken);
    Task<WhatsAppWebhookSignatureValidationResult> ValidateSignatureAsync(byte[] rawBody, string? signatureHeader, CancellationToken cancellationToken);
}