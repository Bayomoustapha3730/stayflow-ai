namespace StayFlow.Api.Services;

public interface IReservationLifecycleEventGenerator
{
    Task<int> GenerateAsync(CancellationToken cancellationToken);
}