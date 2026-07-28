using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed record EarlyCheckInRequestAction(
    Guid ConversationId,
    Guid ReservationId,
    Guid PropertyId,
    TimeOnly? RequestedTime,
    string? GuestNote);

public sealed record LateCheckoutRequestAction(
    Guid ConversationId,
    Guid ReservationId,
    Guid PropertyId,
    TimeOnly? RequestedTime,
    string? GuestNote);

public sealed record MaintenanceTicketAction(
    Guid ConversationId,
    Guid? ReservationId,
    Guid PropertyId,
    MaintenanceCategory Category,
    string Description,
    MaintenanceUrgency Urgency,
    string? Location);

public sealed record HousekeepingRequestAction(
    Guid ConversationId,
    Guid ReservationId,
    Guid PropertyId,
    HousekeepingRequestType RequestType,
    DateOnly? RequestedForDate,
    string? GuestNote);

public sealed record ExtraItemRequestAction(
    Guid ConversationId,
    Guid ReservationId,
    Guid PropertyId,
    ExtraItemType ItemType,
    int Quantity,
    string? GuestNote);

public sealed record ParkingRequestAction(
    Guid ConversationId,
    Guid ReservationId,
    Guid PropertyId,
    int VehicleCount,
    string? VehicleDescription,
    DateOnly? RequestedFrom,
    DateOnly? RequestedTo,
    string? GuestNote);

public sealed record HostNotificationAction(
    Guid ConversationId,
    Guid? ReservationId,
    Guid PropertyId,
    HostNotificationReasonCode ReasonCode,
    HostNotificationPriority Priority,
    string? GuestNote);
