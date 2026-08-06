namespace StayFlow.Api.Models;

public enum SubscriptionStatus
{
    Active = 1,
    Trialing = 2,
    PastDue = 3,
    CancelAtPeriodEnd = 4,
    Suspended = 5,
    Cancelled = 6
}

public static class SubscriptionStatusExtensions
{
    public static string ToStorageValue(this SubscriptionStatus status)
    {
        return status.ToString();
    }
}