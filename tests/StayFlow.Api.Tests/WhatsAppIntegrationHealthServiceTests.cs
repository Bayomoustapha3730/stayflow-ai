using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppIntegrationHealthServiceTests
{
    [Fact]
    public async Task CheckAsync_ValidatedButSendingDisabled_ReturnsProductionPendingWithoutSecrets()
    {
        var cloudClient = new FakeCloudClient { ValidationResult = new WhatsAppValidateIntegrationResult { Success = true } };
        var service = CreateService(cloudClient, new WhatsAppCredentialResolution
        {
            Success = true,
            AccessToken = "access-token",
            AppSecret = "app-secret",
            WebhookVerifyToken = "verify-token"
        }, productionSendingEnabled: false);

        var result = await service.CheckAsync(CreateIntegration(), CancellationToken.None);

        Assert.Equal("ProductionPending", result.Status);
        Assert.False(result.IsSendCapable);
        Assert.DoesNotContain("access-token", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("app-secret", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("verify-token", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, cloudClient.ValidationCallCount);
    }

    [Fact]
    public async Task CheckAsync_MissingWebhookCredentials_ReturnsConfigurationIncompleteWithoutCallingProvider()
    {
        var cloudClient = new FakeCloudClient();
        var service = CreateService(cloudClient, new WhatsAppCredentialResolution
        {
            Success = true,
            AccessToken = "access-token"
        }, productionSendingEnabled: true);

        var result = await service.CheckAsync(CreateIntegration(), CancellationToken.None);

        Assert.Equal("ConfigurationIncomplete", result.Status);
        Assert.False(result.IsSendCapable);
        Assert.Equal(0, cloudClient.ValidationCallCount);
    }

    private static WhatsAppIntegrationHealthService CreateService(FakeCloudClient cloudClient, WhatsAppCredentialResolution credentials, bool productionSendingEnabled)
    {
        return new WhatsAppIntegrationHealthService(
            new FakeCredentialResolver(credentials),
            cloudClient,
            new FakeTelemetry(),
            Options.Create(new WhatsAppCloudOptions { ProductionSendingEnabled = productionSendingEnabled }));
    }

    private static WhatsAppIntegration CreateIntegration() => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        DisplayName = "Production integration",
        PhoneNumberId = "phone-id",
        WhatsAppBusinessAccountId = "waba-id",
        BusinessPhoneNumberMasked = "+1******1234",
        GraphApiVersion = "v23.0",
        IsActive = true,
        IsProductionEnabled = true
    };

    private sealed class FakeCredentialResolver(WhatsAppCredentialResolution result) : IWhatsAppCredentialResolver
    {
        public Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeCloudClient : IWhatsAppCloudClient
    {
        public WhatsAppValidateIntegrationResult ValidationResult { get; init; } = new();
        public int ValidationCallCount { get; private set; }

        public Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken)
        {
            ValidationCallCount++;
            return Task.FromResult(ValidationResult);
        }

        public Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTelemetry : IWhatsAppProviderTelemetry
    {
        public void RecordHealthResult(Guid companyId, Guid integrationId, string status, bool success, long elapsedMilliseconds) { }
        public void RecordRateLimit(Guid companyId, Guid integrationId, string operation, int? retryAfterSeconds) { }
        public void RecordRetry(Guid companyId, Guid integrationId, string operation, int attempt, int? httpStatusCode) { }
        public void RecordSendResult(Guid companyId, Guid integrationId, string messageType, bool success, string? category, int? httpStatusCode, int attempts, long elapsedMilliseconds, string? supportReference) { }
        public void RecordTemplateSyncResult(Guid companyId, Guid integrationId, bool success, string? category, int attempts, long elapsedMilliseconds) { }
    }
}