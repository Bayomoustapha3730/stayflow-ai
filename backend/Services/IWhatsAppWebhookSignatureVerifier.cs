namespace StayFlow.Api.Services;

public interface IWhatsAppWebhookSignatureVerifier
{
    bool IsWebhookVerificationTokenValid(string? providedToken);
    bool TryValidateSignature(byte[] rawBody, string? signatureHeader, out string failureReason);
}