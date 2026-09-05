using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.Controllers;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ConversationsControllerWhatsAppOriginTests
{
    [Fact]
    public async Task AddHostMessage_AssignsManualHostOrigin()
    {
        var conversationService = new RecordingConversationService();
        var controller = new ConversationsController(conversationService, new ThrowingWhatsAppTemplateService());
        var conversationId = Guid.NewGuid();
        var request = new AddHostMessageRequest { Content = "Hello", SentAt = DateTimeOffset.UtcNow };

        var result = await controller.AddHostMessage(conversationId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(conversationId, conversationService.AddHostMessageConversationId);
        Assert.Same(request, conversationService.AddHostMessageRequest);
        Assert.Equal(WhatsAppSendOrigin.ManualHost, conversationService.AddHostMessageOrigin);
    }

    private sealed class RecordingConversationService : IConversationService
    {
        public Guid? AddHostMessageConversationId { get; private set; }
        public AddHostMessageRequest? AddHostMessageRequest { get; private set; }
        public WhatsAppSendOrigin? AddHostMessageOrigin { get; private set; }

        public Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, WhatsAppSendOrigin origin, CancellationToken cancellationToken)
        {
            AddHostMessageConversationId = conversationId;
            AddHostMessageRequest = request;
            AddHostMessageOrigin = origin;
            return Task.FromResult(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse { Id = Guid.NewGuid(), ConversationId = conversationId }, "Stored"));
        }

        public Task<ApiResponse<ConversationListResponse>> GetConversationsAsync(ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationHistoryResponse>> GetConversationHistoryAsync(Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddAIMessageAsync(Guid conversationId, string content, DTOs.AIOrchestration.AIOrchestrationResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> RetryFailedMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddPaymentConfirmationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> UpdateMessageDeliveryStatusAsync(Guid conversationId, Guid messageId, ConversationMessageDeliveryStatus status, DateTimeOffset occurredAt, string? failureCode, string? failureReason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> EscalateConversationAsync(Guid conversationId, EscalateConversationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> EnableHumanTakeoverAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> ReturnToAIModeAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> ResolveConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> CloseConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> AssignConversationToCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationDetailResponse>> UnassignConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<bool>> MarkConversationReadForCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<bool>> MarkConversationReadForGuestAsync(Guid conversationId, Guid guestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ChatMessageFeedbackResponse>> AddGuestMessageFeedbackAsync(Guid conversationId, Guid messageId, AddChatMessageFeedbackRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationFeedbackAnalyticsResponse>> GetFeedbackAnalyticsAsync(ConversationFeedbackAnalyticsQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ThrowingWhatsAppTemplateService : IWhatsAppTemplateService
    {
        public Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppIntegrationDetailResponse>> GetIntegrationDetailAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppIntegrationDetailResponse>> CreateIntegrationAsync(WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppIntegrationDetailResponse>> UpdateIntegrationAsync(Guid integrationId, WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppProductionEnableResponse>> EnableProductionAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppProductionEnableResponse>> DisableProductionAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<ConversationMessageResponse>> SendLifecycleAutomationTemplateMessageAsync(Guid companyId, Guid conversationId, Guid integrationId, Guid templateId, IReadOnlyCollection<string> variables, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}