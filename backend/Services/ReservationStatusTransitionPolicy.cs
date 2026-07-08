using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class ReservationStatusTransitionPolicy : IReservationStatusTransitionPolicy
{
    private static readonly IReadOnlyDictionary<ReservationStatus, IReadOnlySet<ReservationStatus>> ValidTransitions =
        new Dictionary<ReservationStatus, IReadOnlySet<ReservationStatus>>
        {
            [ReservationStatus.Draft] = new HashSet<ReservationStatus>
            {
                ReservationStatus.PendingConfirmation,
                ReservationStatus.Cancelled
            },
            [ReservationStatus.PendingConfirmation] = new HashSet<ReservationStatus>
            {
                ReservationStatus.Confirmed,
                ReservationStatus.Cancelled
            },
            [ReservationStatus.Confirmed] = new HashSet<ReservationStatus>
            {
                ReservationStatus.PreArrival,
                ReservationStatus.Cancelled,
                ReservationStatus.NoShow
            },
            [ReservationStatus.PreArrival] = new HashSet<ReservationStatus>
            {
                ReservationStatus.ReadyForCheckIn,
                ReservationStatus.Cancelled,
                ReservationStatus.NoShow
            },
            [ReservationStatus.ReadyForCheckIn] = new HashSet<ReservationStatus>
            {
                ReservationStatus.CheckedIn,
                ReservationStatus.Cancelled,
                ReservationStatus.NoShow
            },
            [ReservationStatus.CheckedIn] = new HashSet<ReservationStatus>
            {
                ReservationStatus.ActiveStay
            },
            [ReservationStatus.ActiveStay] = new HashSet<ReservationStatus>
            {
                ReservationStatus.CheckOutPending
            },
            [ReservationStatus.CheckOutPending] = new HashSet<ReservationStatus>
            {
                ReservationStatus.CheckedOut
            },
            [ReservationStatus.CheckedOut] = new HashSet<ReservationStatus>
            {
                ReservationStatus.PostStay
            },
            [ReservationStatus.PostStay] = new HashSet<ReservationStatus>
            {
                ReservationStatus.Completed
            }
        };

    public bool CanTransition(ReservationStatus currentStatus, ReservationStatus targetStatus)
    {
        return ValidTransitions.TryGetValue(currentStatus, out var allowedTargets)
            && allowedTargets.Contains(targetStatus);
    }
}
