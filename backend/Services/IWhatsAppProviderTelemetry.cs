namespace StayFlow.Api.Services;

public interface IWhatsAppProviderTelemetry
{
    void RecordSendResult(Guid companyId, Guid integrationId, string messageType, bool success, string? category, int? httpStatusCode, int attempts, long elapsedMilliseconds, string? supportReference);
    void RecordTemplateSyncResult(Guid companyId, Guid integrationId, bool success, string? category, int attempts, long elapsedMilliseconds);
    void RecordRetry(Guid companyId, Guid integrationId, string operation, int attempt, int? httpStatusCode);
    void RecordRateLimit(Guid companyId, Guid integrationId, string operation, int? retryAfterSeconds);
    void RecordHealthResult(Guid companyId, Guid integrationId, string status, bool success, long elapsedMilliseconds);
}
