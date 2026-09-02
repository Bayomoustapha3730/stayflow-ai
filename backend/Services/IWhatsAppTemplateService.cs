using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public interface IWhatsAppTemplateService
{
    Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppIntegrationDetailResponse>> GetIntegrationDetailAsync(Guid integrationId, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppIntegrationDetailResponse>> CreateIntegrationAsync(WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppIntegrationDetailResponse>> UpdateIntegrationAsync(Guid integrationId, WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppProductionEnableResponse>> EnableProductionAsync(Guid integrationId, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppProductionEnableResponse>> DisableProductionAsync(Guid integrationId, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken);
    // Trusted, service-to-service entry point for automated lifecycle sends (no HTTP tenant
    // context, no human-takeover requirement). companyId/integrationId come from an
    // already-verified caller (GuestJourneyMessageDeliveryProcessor), not tenant HTTP context.
    Task<ApiResponse<ConversationMessageResponse>> SendLifecycleAutomationTemplateMessageAsync(
        Guid companyId,
        Guid conversationId,
        Guid integrationId,
        Guid templateId,
        IReadOnlyCollection<string> variables,
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken);
}
