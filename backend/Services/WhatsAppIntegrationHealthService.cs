using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WhatsAppIntegrationHealthService(
    IWhatsAppCredentialResolver credentialResolver,
    IWhatsAppCloudClient whatsAppCloudClient,
    IHostEnvironment environment) : IWhatsAppIntegrationHealthService
{
    public async Task<WhatsAppIntegrationHealthResponse> CheckAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;

        if (!integration.IsActive)
        {
            return CreateResponse(integration.Id, "Disabled", "Integration is inactive.", false, checkedAt);
        }

        if (!integration.IsProductionEnabled)
        {
            return CreateResponse(integration.Id, "DevelopmentOnly", "Production sending is not enabled.", environment.IsDevelopment(), checkedAt);
        }

        if (string.IsNullOrWhiteSpace(integration.PhoneNumberId)
            || string.IsNullOrWhiteSpace(integration.WhatsAppBusinessAccountId)
            || string.IsNullOrWhiteSpace(integration.GraphApiVersion))
        {
            return CreateResponse(integration.Id, "ConfigurationIncomplete", "Integration configuration is incomplete.", false, checkedAt);
        }

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return CreateResponse(integration.Id, "ConfigurationIncomplete", credentials.FailureSummary ?? "Integration credentials are missing.", false, checkedAt);
        }

        var verification = await whatsAppCloudClient.ValidateIntegrationAsync(new DTOs.WhatsApp.WhatsAppValidateIntegrationRequest
        {
            AccessToken = credentials.AccessToken,
            GraphApiVersion = integration.GraphApiVersion,
            WhatsAppBusinessAccountId = integration.WhatsAppBusinessAccountId
        }, cancellationToken);

        if (verification.Success)
        {
            return CreateResponse(integration.Id, "Healthy", "Integration is healthy.", true, checkedAt);
        }

        var status = verification.IsTransientFailure ? "ProviderUnavailable" : "AuthenticationFailed";
        var message = verification.IsTransientFailure
            ? "WhatsApp provider is temporarily unavailable."
            : "WhatsApp credentials were rejected by the provider.";
        return CreateResponse(integration.Id, status, message, false, checkedAt);
    }

    private static WhatsAppIntegrationHealthResponse CreateResponse(Guid integrationId, string status, string message, bool isSendCapable, DateTimeOffset checkedAt)
    {
        return new WhatsAppIntegrationHealthResponse
        {
            IntegrationId = integrationId,
            Status = status,
            Message = message,
            IsSendCapable = isSendCapable,
            CheckedAt = checkedAt
        };
    }
}
