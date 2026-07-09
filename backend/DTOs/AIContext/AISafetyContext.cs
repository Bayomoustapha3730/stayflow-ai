namespace StayFlow.Api.DTOs.AIContext;

public sealed class AISafetyContext
{
    public bool RequiresPropertyAccessAuthorization { get; init; }
    public bool ReservationContextResolved { get; init; }
    public bool TenantValidated { get; init; }
    public bool GuestValidated { get; init; }
    public bool ContextMinimized { get; init; } = true;
}
