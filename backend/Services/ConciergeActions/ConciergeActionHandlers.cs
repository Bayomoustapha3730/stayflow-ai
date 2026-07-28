using System.Text.Json;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class EarlyCheckInRequestHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<EarlyCheckInRequestAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.RequestEarlyCheckIn;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, EarlyCheckInRequestAction action, CancellationToken cancellationToken)
    {
        var entity = new EarlyCheckInRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            RequestedTime = action.RequestedTime,
            GuestNote = action.GuestNote,
            Status = EarlyCheckInRequestStatus.Pending
        };

        await dbContext.EarlyCheckInRequests.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.AwaitingHostApproval, true, false, entity.Id, true, true, ConciergeActionResponseCodes.EarlyCheckInRequestSubmitted, null, DateTimeOffset.UtcNow);
    }
}

public sealed class LateCheckoutRequestHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<LateCheckoutRequestAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.RequestLateCheckout;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, LateCheckoutRequestAction action, CancellationToken cancellationToken)
    {
        var entity = new LateCheckoutRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            RequestedTime = action.RequestedTime,
            GuestNote = action.GuestNote,
            Status = LateCheckoutRequestStatus.Pending
        };

        await dbContext.LateCheckoutRequests.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.AwaitingHostApproval, true, false, entity.Id, true, true, ConciergeActionResponseCodes.LateCheckoutRequestSubmitted, null, DateTimeOffset.UtcNow);
    }
}

public sealed class MaintenanceTicketHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<MaintenanceTicketAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.CreateMaintenanceTicket;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, MaintenanceTicketAction action, CancellationToken cancellationToken)
    {
        var entity = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            Category = action.Category,
            DescriptionSummary = action.Description,
            Urgency = action.Urgency,
            Location = action.Location,
            Status = MaintenanceTicketStatus.Open
        };

        await dbContext.MaintenanceTickets.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.Completed, true, false, entity.Id, false, true, ConciergeActionResponseCodes.MaintenanceTicketCreated, null, DateTimeOffset.UtcNow);
    }
}

public sealed class HousekeepingRequestHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<HousekeepingRequestAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.RequestHousekeeping;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, HousekeepingRequestAction action, CancellationToken cancellationToken)
    {
        var entity = new HousekeepingRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            RequestType = action.RequestType,
            RequestedForDate = action.RequestedForDate,
            GuestNote = action.GuestNote,
            Status = HousekeepingRequestStatus.Pending
        };

        await dbContext.HousekeepingRequests.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.Completed, true, false, entity.Id, false, true, ConciergeActionResponseCodes.HousekeepingRequestSubmitted, null, DateTimeOffset.UtcNow);
    }
}

public sealed class ExtraItemRequestHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<ExtraItemRequestAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.RequestExtraItem;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, ExtraItemRequestAction action, CancellationToken cancellationToken)
    {
        var entity = new ExtraItemRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            ItemType = action.ItemType,
            Quantity = action.Quantity,
            GuestNote = action.GuestNote,
            Status = ExtraItemRequestStatus.Pending
        };

        await dbContext.ExtraItemRequests.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.Completed, true, false, entity.Id, false, true, ConciergeActionResponseCodes.ExtraItemRequestSubmitted, null, DateTimeOffset.UtcNow);
    }
}

public sealed class ParkingRequestHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<ParkingRequestAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.RequestParking;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, ParkingRequestAction action, CancellationToken cancellationToken)
    {
        var entity = new ParkingRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            VehicleCount = action.VehicleCount,
            VehicleDescription = action.VehicleDescription,
            RequestedFromDate = action.RequestedFrom,
            RequestedToDate = action.RequestedTo,
            GuestNote = action.GuestNote,
            Status = ParkingRequestStatus.Pending
        };

        await dbContext.ParkingRequests.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.AwaitingHostApproval, true, false, entity.Id, true, true, ConciergeActionResponseCodes.ParkingRequestSubmitted, null, DateTimeOffset.UtcNow);
    }
}

public sealed class HostNotificationHandler(ApplicationDbContext dbContext) : IConciergeActionHandler<HostNotificationAction>
{
    public ConciergeActionType ActionType => ConciergeActionType.NotifyHost;

    public async Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, HostNotificationAction action, CancellationToken cancellationToken)
    {
        var entity = new HostNotificationRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = (await dbContext.PendingConciergeActions.FindAsync([pendingActionId], cancellationToken))!.CompanyId,
            PropertyId = action.PropertyId,
            ReservationId = action.ReservationId,
            ConversationId = action.ConversationId,
            ReasonCode = action.ReasonCode,
            Priority = action.Priority,
            GuestNote = action.GuestNote
        };

        await dbContext.HostNotificationRecords.AddAsync(entity, cancellationToken);
        return new ConciergeActionExecutionResult(pendingActionId, ActionType, PendingConciergeActionStatus.Completed, true, false, entity.Id, false, true, ConciergeActionResponseCodes.HostNotified, null, DateTimeOffset.UtcNow);
    }
}

public static class ConciergeActionSerialization
{
    public static string Serialize(object value) => JsonSerializer.Serialize(value);

    public static object Deserialize(ConciergeActionType actionType, string payload)
    {
        return actionType switch
        {
            ConciergeActionType.RequestEarlyCheckIn => JsonSerializer.Deserialize<EarlyCheckInRequestAction>(payload)!,
            ConciergeActionType.RequestLateCheckout => JsonSerializer.Deserialize<LateCheckoutRequestAction>(payload)!,
            ConciergeActionType.CreateMaintenanceTicket => JsonSerializer.Deserialize<MaintenanceTicketAction>(payload)!,
            ConciergeActionType.RequestHousekeeping => JsonSerializer.Deserialize<HousekeepingRequestAction>(payload)!,
            ConciergeActionType.RequestExtraItem => JsonSerializer.Deserialize<ExtraItemRequestAction>(payload)!,
            ConciergeActionType.RequestParking => JsonSerializer.Deserialize<ParkingRequestAction>(payload)!,
            ConciergeActionType.NotifyHost => JsonSerializer.Deserialize<HostNotificationAction>(payload)!,
            _ => throw new InvalidOperationException("Unsupported action payload.")
        };
    }
}
