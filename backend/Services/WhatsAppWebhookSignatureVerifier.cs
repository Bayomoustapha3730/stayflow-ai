using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class WhatsAppWebhookSignatureVerifier(
    IOptions<WhatsAppCloudOptions> options,
    IWhatsAppRepository whatsAppRepository,
    IWhatsAppCredentialResolver credentialResolver) : IWhatsAppWebhookSignatureVerifier
{
    public async Task<bool> IsWebhookVerificationTokenValidAsync(string? providedToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        var expectedTokens = await ResolveCandidateWebhookTokensAsync(cancellationToken);
        if (expectedTokens.Count == 0)
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedToken.Trim());
        var matched = false;

        foreach (var expected in expectedTokens)
        {
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var isLengthMatch = expectedBytes.Length == providedBytes.Length;
            var comparison = isLengthMatch && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
            matched |= comparison;
        }

        return matched;
    }

    public async Task<WhatsAppWebhookSignatureValidationResult> ValidateSignatureAsync(byte[] rawBody, string? signatureHeader, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return new WhatsAppWebhookSignatureValidationResult
            {
                IsValid = false,
                FailureReason = "MissingSignature"
            };
        }

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return new WhatsAppWebhookSignatureValidationResult
            {
                IsValid = false,
                FailureReason = "InvalidSignatureFormat"
            };
        }

        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromHexString(signatureHeader[prefix.Length..].Trim());
        }
        catch (FormatException)
        {
            return new WhatsAppWebhookSignatureValidationResult
            {
                IsValid = false,
                FailureReason = "InvalidSignatureFormat"
            };
        }

        var candidates = await ResolveCandidateAppSecretsAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return new WhatsAppWebhookSignatureValidationResult
            {
                IsValid = false,
                FailureReason = "NoConfiguredAppSecret"
            };
        }

        var matched = false;
        foreach (var secret in candidates)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = hmac.ComputeHash(rawBody);
            var isMatch = computed.Length == providedSignature.Length && CryptographicOperations.FixedTimeEquals(computed, providedSignature);
            matched |= isMatch;
        }

        return new WhatsAppWebhookSignatureValidationResult
        {
            IsValid = matched,
            FailureReason = matched ? string.Empty : "InvalidSignature"
        };
    }

    private async Task<IReadOnlyCollection<string>> ResolveCandidateAppSecretsAsync(CancellationToken cancellationToken)
    {
        var integrations = await whatsAppRepository.ListActiveIntegrationsAsync(cancellationToken);
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        var defaultResolution = await credentialResolver.ResolveAsync(new Models.WhatsAppIntegration
        {
            CredentialReference = options.Value.DefaultCredentialReference
        }, cancellationToken);
        if (defaultResolution.Success && !string.IsNullOrWhiteSpace(defaultResolution.AppSecret))
        {
            candidates.Add(defaultResolution.AppSecret.Trim());
        }

        foreach (var integration in integrations)
        {
            var resolution = await credentialResolver.ResolveAsync(integration, cancellationToken);
            if (resolution.Success && !string.IsNullOrWhiteSpace(resolution.AppSecret))
            {
                candidates.Add(resolution.AppSecret.Trim());
            }
        }

        return candidates.ToList();
    }

    private async Task<IReadOnlyCollection<string>> ResolveCandidateWebhookTokensAsync(CancellationToken cancellationToken)
    {
        var integrations = await whatsAppRepository.ListActiveIntegrationsAsync(cancellationToken);
        var candidates = new HashSet<string>(StringComparer.Ordinal);

        var defaultResolution = await credentialResolver.ResolveAsync(new Models.WhatsAppIntegration
        {
            CredentialReference = options.Value.DefaultCredentialReference
        }, cancellationToken);
        if (defaultResolution.Success && !string.IsNullOrWhiteSpace(defaultResolution.WebhookVerifyToken))
        {
            candidates.Add(defaultResolution.WebhookVerifyToken.Trim());
        }

        foreach (var integration in integrations)
        {
            var resolution = await credentialResolver.ResolveAsync(integration, cancellationToken);
            if (resolution.Success && !string.IsNullOrWhiteSpace(resolution.WebhookVerifyToken))
            {
                candidates.Add(resolution.WebhookVerifyToken.Trim());
            }
        }

        return candidates.ToList();
    }
}
