namespace StayFlow.Api.Services;

internal sealed class WhatsAppProviderContext
{
    public Guid CompanyId { get; init; }
    public Guid IntegrationId { get; init; }
    public string PhoneNumberId { get; init; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; init; } = string.Empty;
    public string GraphApiVersion { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
}
