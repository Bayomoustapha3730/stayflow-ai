using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using System.Text;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCredentialResolver(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<WhatsAppCloudOptions> options) : IWhatsAppCredentialResolver
{
    private const string Prefix = "STAYFLOW_WHATSAPP_";

    public Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
    {
        var reference = string.IsNullOrWhiteSpace(integration.CredentialReference)
            ? options.Value.DefaultCredentialReference
            : integration.CredentialReference.Trim();

        if (!TryNormalizeReference(reference, out var normalizedReference))
        {
            return Task.FromResult(new WhatsAppCredentialResolution
            {
                Success = false,
                FailureCode = "InvalidCredentialReference",
                FailureSummary = "WhatsApp credential configuration is invalid for this integration."
            });
        }

        var section = configuration.GetSection($"WhatsAppCredentials:{reference}");

        var accessToken = FirstNonEmpty(
            configuration[$"{Prefix}{normalizedReference}_ACCESS_TOKEN"],
            Environment.GetEnvironmentVariable($"{Prefix}{normalizedReference}_ACCESS_TOKEN"),
            section["AccessToken"]);

        var appSecret = FirstNonEmpty(
            configuration[$"{Prefix}{normalizedReference}_APP_SECRET"],
            Environment.GetEnvironmentVariable($"{Prefix}{normalizedReference}_APP_SECRET"),
            section["AppSecret"]);

        var webhookVerifyToken = FirstNonEmpty(
            configuration[$"{Prefix}{normalizedReference}_WEBHOOK_VERIFY_TOKEN"],
            Environment.GetEnvironmentVariable($"{Prefix}{normalizedReference}_WEBHOOK_VERIFY_TOKEN"),
            section["WebhookVerifyToken"]);

        if (environment.IsDevelopment())
        {
            accessToken ??= "dev-access-token";
            appSecret ??= "dev-app-secret";
            webhookVerifyToken ??= "dev-verify-token";
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(new WhatsAppCredentialResolution
            {
                Success = false,
                FailureCode = "MissingAccessToken",
                FailureSummary = "WhatsApp access token is missing for this integration."
            });
        }

        if (string.IsNullOrWhiteSpace(appSecret))
        {
            return Task.FromResult(new WhatsAppCredentialResolution
            {
                Success = false,
                FailureCode = "MissingAppSecret",
                FailureSummary = "WhatsApp app secret is missing for this integration."
            });
        }

        if (string.IsNullOrWhiteSpace(webhookVerifyToken))
        {
            return Task.FromResult(new WhatsAppCredentialResolution
            {
                Success = false,
                FailureCode = "MissingWebhookVerifyToken",
                FailureSummary = "WhatsApp webhook verify token is missing for this integration."
            });
        }

        return Task.FromResult(new WhatsAppCredentialResolution
        {
            Success = true,
            AccessToken = accessToken.Trim(),
            AppSecret = appSecret.Trim(),
            WebhookVerifyToken = webhookVerifyToken.Trim()
        });
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryNormalizeReference(string? reference, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var builder = new StringBuilder(reference.Length);
        foreach (var ch in reference.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
            else
            {
                builder.Append('_');
            }
        }

        var candidate = builder.ToString().Trim('_');
        while (candidate.Contains("__", StringComparison.Ordinal))
        {
            candidate = candidate.Replace("__", "_", StringComparison.Ordinal);
        }

        if (candidate.Length is < 1 or > 64)
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
