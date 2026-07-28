namespace StayFlow.Api.Services;

public sealed class WhatsAppProviderTelemetry(ILogger<WhatsAppProviderTelemetry> logger) : IWhatsAppProviderTelemetry
{
    public void RecordSendResult(Guid companyId, Guid integrationId, string messageType, bool success, string? category, int? httpStatusCode, int attempts, long elapsedMilliseconds, string? supportReference)
    {
        logger.LogInformation(
            "WhatsApp provider send completed. CompanyId={CompanyId} IntegrationId={IntegrationId} MessageType={MessageType} Success={Success} Category={Category} HttpStatusCode={HttpStatusCode} Attempts={Attempts} ElapsedMs={ElapsedMs} SupportRef={SupportRef}",
            companyId,
            integrationId,
            messageType,
            success,
            category,
            httpStatusCode,
            attempts,
            elapsedMilliseconds,
            supportReference);
    }

    public void RecordTemplateSyncResult(Guid companyId, Guid integrationId, bool success, string? category, int attempts, long elapsedMilliseconds)
    {
        logger.LogInformation(
            "WhatsApp template sync completed. CompanyId={CompanyId} IntegrationId={IntegrationId} Success={Success} Category={Category} Attempts={Attempts} ElapsedMs={ElapsedMs}",
            companyId,
            integrationId,
            success,
            category,
            attempts,
            elapsedMilliseconds);
    }

    public void RecordRetry(Guid companyId, Guid integrationId, string operation, int attempt, int? httpStatusCode)
    {
        logger.LogInformation(
            "WhatsApp provider retry scheduled. CompanyId={CompanyId} IntegrationId={IntegrationId} Operation={Operation} Attempt={Attempt} HttpStatusCode={HttpStatusCode}",
            companyId,
            integrationId,
            operation,
            attempt,
            httpStatusCode);
    }

    public void RecordRateLimit(Guid companyId, Guid integrationId, string operation, int? retryAfterSeconds)
    {
        logger.LogInformation(
            "WhatsApp provider rate limit observed. CompanyId={CompanyId} IntegrationId={IntegrationId} Operation={Operation} RetryAfterSeconds={RetryAfterSeconds}",
            companyId,
            integrationId,
            operation,
            retryAfterSeconds);
    }

    public void RecordHealthResult(Guid companyId, Guid integrationId, string status, bool success, long elapsedMilliseconds)
    {
        logger.LogInformation(
            "WhatsApp health result. CompanyId={CompanyId} IntegrationId={IntegrationId} Status={Status} Success={Success} ElapsedMs={ElapsedMs}",
            companyId,
            integrationId,
            status,
            success,
            elapsedMilliseconds);
    }
}
