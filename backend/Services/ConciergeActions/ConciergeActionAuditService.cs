using System.Text.Json;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionAuditService(ApplicationDbContext dbContext) : IConciergeActionAuditService
{
    public async Task WriteAsync(
        Guid companyId,
        Guid conversationId,
        Guid? pendingActionId,
        ConciergeActionType actionType,
        ConciergeActionAuditEventType eventType,
        string actorType,
        Guid? actorUserId,
        string channel,
        string resultCode,
        string correlationId,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var payload = metadata is null ? null : JsonSerializer.Serialize(metadata);
        if (payload is { Length: > 1500 })
        {
            payload = payload[..1500];
        }

        await dbContext.ConciergeActionAuditLogs.AddAsync(new ConciergeActionAuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversationId,
            PendingActionId = pendingActionId,
            ActionType = actionType,
            EventType = eventType,
            ActorType = actorType,
            ActorUserId = actorUserId,
            Channel = channel,
            ResultCode = resultCode,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? "none" : correlationId.Trim(),
            MetadataJson = payload,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
