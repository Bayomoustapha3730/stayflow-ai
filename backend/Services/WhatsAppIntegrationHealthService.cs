using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class WhatsAppIntegrationHealthService(
    IWhatsAppCredentialResolver credentialResolver,
    IWhatsAppCloudClient whatsAppCloudClient,
    IWhatsAppProviderTelemetry telemetry,
    IOptions<WhatsAppCloudOptions> options,
    IHostEnvironment environment) : IWhatsAppIntegrationHealthService
{
    public async Task<WhatsAppIntegrationHealthResponse> CheckAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var checkedAt = DateTimeOffset.UtcNow;

        if (!integration.IsActive)
        {
            return Complete("Disabled", "Integration is inactive.", false);
        }

        if (!integration.IsProductionEnabled || !options.Value.ProductionSendingEnabled)
        {
            return Complete("DevelopmentOnly", "Production sending is not enabled.", environment.IsDevelopment());
        }

        if (string.IsNullOrWhiteSpace(integration.PhoneNumberId)
            || string.IsNullOrWhiteSpace(integration.WhatsAppBusinessAccountId)
            || string.IsNullOrWhiteSpace(integration.GraphApiVersion))
        {
            return Complete("ConfigurationIncomplete", "Integration configuration is incomplete.", false);
        }

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return Complete("ConfigurationIncomplete", credentials.FailureSummary ?? "Integration credentials are missing.", false);
        }

        var verification = await whatsAppCloudClient.ValidateIntegrationAsync(new DTOs.WhatsApp.WhatsAppValidateIntegrationRequest
        {
            CompanyId = integration.CompanyId,
            IntegrationId = integration.Id,
            AccessToken = credentials.AccessToken,
            GraphApiVersion = integration.GraphApiVersion,
            WhatsAppBusinessAccountId = integration.WhatsAppBusinessAccountId
        }, cancellationToken);

        if (verification.Success)
        {
            return Complete("Healthy", "Integration is healthy.", true);
        }

        var status = verification.FailureCategory switch
        {
            "Authentication" => "AuthenticationFailed",
            "Authorization" => "AuthorizationFailed",
            "RateLimited" => "RateLimited",
            "ProviderUnavailable" => "ProviderUnavailable",
            "TemporaryProviderFailure" => "ProviderUnavailable",
            "Configuration" => "ConfigurationIncomplete",
            _ => verification.IsTransientFailure ? "ProviderUnavailable" : "ConfigurationIncomplete"
        };

        var message = status switch
        {
            "AuthenticationFailed" => "WhatsApp credentials were rejected by the provider.",
            "AuthorizationFailed" => "WhatsApp integration is not authorized for this operation.",
            "RateLimited" => "WhatsApp is temporarily rate limited. Try again shortly.",
            "ProviderUnavailable" => "WhatsApp provider is temporarily unavailable.",
            _ => "Integration configuration is incomplete."
        };

        return Complete(status, message, false);

        WhatsAppIntegrationHealthResponse Complete(string status, string message, bool isSendCapable)
        {
            var elapsed = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            telemetry.RecordHealthResult(integration.CompanyId, integration.Id, status, isSendCapable, elapsed);

            return new WhatsAppIntegrationHealthResponse
            {
                IntegrationId = integration.Id,
                Status = status,
                Message = message,
                IsSendCapable = isSendCapable,
                CheckedAt = checkedAt
            };
        }
    }
}
