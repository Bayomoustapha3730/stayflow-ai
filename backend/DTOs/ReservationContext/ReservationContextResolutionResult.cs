namespace StayFlow.Api.DTOs.ReservationContext;

public sealed class ReservationContextResolutionResult
{
    public ReservationContextResolutionOutcome Outcome { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }
    public ReservationContextResolutionMethod? ResolutionMethod { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public IReadOnlyCollection<ReservationCandidateLabel> CandidateLabels { get; init; } = [];
    public string? EscalationReason { get; init; }
    public string? Message { get; init; }

    public static ReservationContextResolutionResult Resolved(
        Guid companyId,
        Guid guestId,
        Guid reservationId,
        Guid propertyId,
        ReservationContextResolutionMethod method,
        DateTimeOffset resolvedAt)
    {
        return new ReservationContextResolutionResult
        {
            Outcome = ReservationContextResolutionOutcome.Resolved,
            CompanyId = companyId,
            GuestId = guestId,
            ReservationId = reservationId,
            PropertyId = propertyId,
            ResolutionMethod = method,
            ResolvedAt = resolvedAt
        };
    }
}
