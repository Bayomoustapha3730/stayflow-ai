using StayFlow.Api.Common;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed record HostActionListItem(
    Guid ActionId,
    ConciergeActionType ActionType,
    PendingConciergeActionStatus Status,
    Guid ConversationId,
    Guid PropertyId,
    Guid? ReservationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExecutedAt);

public interface IConciergeHostActionService
{
    Task<ApiResponse<PagedResult<HostActionListItem>>> ListAsync(Guid companyId, Guid? propertyId, PendingConciergeActionStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<ApiResponse<HostActionListItem>> ApproveAsync(Guid companyId, Guid actionId, Guid userId, string? note, CancellationToken cancellationToken);
    Task<ApiResponse<HostActionListItem>> DeclineAsync(Guid companyId, Guid actionId, Guid userId, string? note, CancellationToken cancellationToken);
}
