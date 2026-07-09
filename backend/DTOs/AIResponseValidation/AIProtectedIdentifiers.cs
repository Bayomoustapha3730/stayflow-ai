namespace StayFlow.Api.DTOs.AIResponseValidation;

public sealed class AIProtectedIdentifiers
{
    public Guid? CompanyId { get; init; }
    public Guid? GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }

    public IEnumerable<Guid> Values()
    {
        if (CompanyId is { } companyId)
        {
            yield return companyId;
        }

        if (GuestId is { } guestId)
        {
            yield return guestId;
        }

        if (ReservationId is { } reservationId)
        {
            yield return reservationId;
        }

        if (PropertyId is { } propertyId)
        {
            yield return propertyId;
        }
    }
}
