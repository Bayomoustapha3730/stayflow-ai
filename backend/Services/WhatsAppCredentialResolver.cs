using Microsoft.Extensions.Options;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCredentialResolver(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<WhatsAppCloudOptions> options) : IWhatsAppCredentialResolver
{
    public Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
    {
        var reference = string.IsNullOrWhiteSpace(integration.CredentialReference)
            ? options.Value.DefaultCredentialReference
            : integration.CredentialReference.Trim();

        var section = configuration.GetSection($"WhatsAppCredentials:{reference}");

        var accessToken = section["AccessToken"];
        var appSecret = section["AppSecret"];
        var webhookVerifyToken = section["WebhookVerifyToken"];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken = options.Value.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(appSecret))
        {
            appSecret = options.Value.AppSecret;
        }

        if (string.IsNullOrWhiteSpace(webhookVerifyToken))
        {
            webhookVerifyToken = options.Value.WebhookVerifyToken;
        }

        if (environment.IsDevelopment())
        {
            accessToken ??= "dev-access-token";
            appSecret ??= "dev-app-secret";
            webhookVerifyToken ??= "dev-verify-token";
        }

        if (string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(appSecret)
            || string.IsNullOrWhiteSpace(webhookVerifyToken))
        {
            return Task.FromResult(new WhatsAppCredentialResolution
            {
                Success = false,
                FailureSummary = "WhatsApp credentials are missing for this integration."
            });
        }

        return Task.FromResult(new WhatsAppCredentialResolution
        {
            Success = true,
            AccessToken = accessToken,
            AppSecret = appSecret,
            WebhookVerifyToken = webhookVerifyToken
        });
    }
}
