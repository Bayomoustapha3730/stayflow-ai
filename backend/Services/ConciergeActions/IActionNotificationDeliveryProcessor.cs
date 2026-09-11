namespace StayFlow.Api.Services.ConciergeActions;

public sealed record ActionNotificationDeliveryResult(
    int Claimed,
    int Sent,
    int Failed,
    int Skipped);

public interface IActionNotificationDeliveryProcessor
{
    Task<ActionNotificationDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken);
}
