namespace StayFlow.Api.Models;

public enum UsageMetric
{
    Users = 1,
    Properties = 2,
    Reservations = 3,
    AiRequests = 4,
    AiTokens = 5,
    WhatsAppMessages = 6,
    ApiRequests = 7,
    StorageBytes = 8,
    FileUploads = 9
}

public static class UsageMetricExtensions
{
    public static string ToStorageValue(this UsageMetric metric)
    {
        return metric.ToString();
    }

    public static string ToQuotaEntitlementKey(this UsageMetric metric)
    {
        return $"Quota.{metric}";
    }
}