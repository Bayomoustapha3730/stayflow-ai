using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IReservationLifecycleEventIdempotencyKeyBuilder
{
    string Build(Guid companyId, Guid reservationId, ReservationLifecycleEventType eventType, DateOnly propertyLocalDate, string ruleVersion);
}
