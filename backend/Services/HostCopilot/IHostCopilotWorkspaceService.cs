using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Services.HostCopilot;

public interface IHostCopilotWorkspaceService
{
    Task<ApiResponse<HostCopilotWorkspaceResponse>> GetWorkspaceAsync(Guid? propertyId, CancellationToken cancellationToken);
    Task<ApiResponse<HostCopilotDraftResponse>> GenerateDraftAsync(Guid conversationId, HostCopilotDraftGenerateRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<HostCopilotDraftValidationResponse>> ValidateDraftAsync(Guid conversationId, HostCopilotDraftValidateRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationMessageResponse>> SendDraftAsync(Guid conversationId, HostCopilotDraftSendRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<HostActionListItem>> ApprovePendingActionAsync(Guid actionId, HostActionDecisionRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<HostActionListItem>> DeclinePendingActionAsync(Guid actionId, HostActionDecisionRequest request, CancellationToken cancellationToken);
}
