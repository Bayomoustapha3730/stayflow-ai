namespace StayFlow.Api.Services;

public sealed record ReservationLifecycleEventProcessingResult(int StaleRecovered, int FailedRecovered, int Claimed, int Processed, int Failed, int Suppressed);

public interface IReservationLifecycleEventProcessor
{
    Task<ReservationLifecycleEventProcessingResult> ProcessDueAsync(CancellationToken cancellationToken);
}