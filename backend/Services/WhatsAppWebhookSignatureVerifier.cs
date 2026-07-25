using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class WhatsAppWebhookSignatureVerifier(IOptions<WhatsAppCloudOptions> options) : IWhatsAppWebhookSignatureVerifier
{
    public bool IsWebhookVerificationTokenValid(string? providedToken)
    {
        return FixedTimeEquals(options.Value.WebhookVerifyToken, providedToken);
    }

    public bool TryValidateSignature(byte[] rawBody, string? signatureHeader, out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            failureReason = "MissingSignature";
            return false;
        }

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "InvalidSignatureFormat";
            return false;
        }

        var signatureHex = signatureHeader[prefix.Length..].Trim();
        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            failureReason = "InvalidSignatureFormat";
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Value.AppSecret));
        var computedSignature = hmac.ComputeHash(rawBody);
        if (!CryptographicOperations.FixedTimeEquals(computedSignature, providedSignature))
        {
            failureReason = "InvalidSignature";
            return false;
        }

        return true;
    }

    private static bool FixedTimeEquals(string? expected, string? provided)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var left = Encoding.UTF8.GetBytes(expected.Trim());
        var right = Encoding.UTF8.GetBytes(provided.Trim());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}