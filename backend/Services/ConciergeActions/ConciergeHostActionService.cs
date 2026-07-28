using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeHostActionService(
    ApplicationDbContext dbContext,
    IConciergeActionAuditService auditService,
    ICurrentTenantContext tenantContext) : IConciergeHostActionService
{
    public async Task<ApiResponse<PagedResult<HostActionListItem>>> ListAsync(Guid companyId, Guid? propertyId, PendingConciergeActionStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.PendingConciergeActions.AsNoTracking().Where(item => item.CompanyId == companyId);
        if (propertyId.HasValue)
        {
            query = query.Where(item => item.PropertyId == propertyId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new HostActionListItem(
                item.Id,
                item.ActionType,
                item.Status,
                item.ConversationId,
                item.PropertyId,
                item.ReservationId,
                item.CreatedAt,
                item.ExecutedAt))
            .ToListAsync(cancellationToken);

        return ApiResponse<PagedResult<HostActionListItem>>.Ok(new PagedResult<HostActionListItem>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public Task<ApiResponse<HostActionListItem>> ApproveAsync(Guid companyId, Guid actionId, Guid userId, string? note, CancellationToken cancellationToken)
        => DecideAsync(companyId, actionId, userId, note, true, cancellationToken);

    public Task<ApiResponse<HostActionListItem>> DeclineAsync(Guid companyId, Guid actionId, Guid userId, string? note, CancellationToken cancellationToken)
        => DecideAsync(companyId, actionId, userId, note, false, cancellationToken);

    private async Task<ApiResponse<HostActionListItem>> DecideAsync(Guid companyId, Guid actionId, Guid userId, string? note, bool approve, CancellationToken cancellationToken)
    {
        var item = await dbContext.PendingConciergeActions.FirstOrDefaultAsync(entry => entry.CompanyId == companyId && entry.Id == actionId, cancellationToken);
        if (item is null)
        {
            return ApiResponse<HostActionListItem>.Fail("Action was not found.");
        }

        if (item.Status == PendingConciergeActionStatus.Completed)
        {
            return ApiResponse<HostActionListItem>.Ok(Map(item), "Action was already decided.");
        }

        if (item.Status != PendingConciergeActionStatus.AwaitingHostApproval)
        {
            return ApiResponse<HostActionListItem>.Fail("Action is not awaiting host approval.");
        }

        item.Status = approve ? PendingConciergeActionStatus.Completed : PendingConciergeActionStatus.Cancelled;
        item.ExecutedAt = DateTimeOffset.UtcNow;
        item.FailureReasonCode = approve ? null : "HostDeclined";

        await auditService.WriteAsync(
            companyId,
            item.ConversationId,
            item.Id,
            item.ActionType,
            approve ? ConciergeActionAuditEventType.HostApproved : ConciergeActionAuditEventType.HostDeclined,
            "HostUser",
            userId,
            "Host",
            approve ? "HostApproved" : "HostDeclined",
            tenantContext.CorrelationId ?? "none",
            new { Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 180)] },
            cancellationToken);

        await dbContext.ActionNotificationOutbox.AddAsync(new ActionNotificationOutbox
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ActionId = item.Id,
            NotificationType = approve ? "HostApproved" : "HostDeclined",
            PayloadReference = $"action:{item.Id:N}",
            Status = ActionNotificationOutboxStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<HostActionListItem>.Ok(Map(item));
    }

    private static HostActionListItem Map(PendingConciergeAction item)
    {
        return new HostActionListItem(item.Id, item.ActionType, item.Status, item.ConversationId, item.PropertyId, item.ReservationId, item.CreatedAt, item.ExecutedAt);
    }
}
