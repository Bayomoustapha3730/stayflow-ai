using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCloudOptions
{
    public const string SectionName = "WhatsAppCloud";

    public bool Enabled { get; set; }
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";
    public string GraphApiVersion { get; set; } = string.Empty;
    public string DefaultCredentialReference { get; set; } = "default";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 2;
    public int MaxPostRetryAttempts { get; set; } = 0;
    public int RetryBaseDelayMilliseconds { get; set; } = 250;
    public int RetryMaxDelaySeconds { get; set; } = 8;
    public int MaxTemplateSyncPages { get; set; } = 10;
    public int MaxTemplateSyncItems { get; set; } = 500;
    public bool ProductionSendingEnabled { get; set; }
    // Authorizes production sending for WhatsAppSendOrigin.ManualHost only; never unblocks autonomous origins.
    public bool ManualHostProductionSendingEnabled { get; set; }
    public bool DevelopmentMode { get; set; }
    public int CustomerServiceWindowHours { get; set; } = 24;
}

public sealed class WhatsAppCloudOptionsValidator : IValidateOptions<WhatsAppCloudOptions>
{
    public ValidateOptionsResult Validate(string? name, WhatsAppCloudOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.GraphApiBaseUrl))
        {
            errors.Add("WhatsAppCloud:GraphApiBaseUrl is required when WhatsAppCloud:Enabled is true.");
        }
        else if (!Uri.TryCreate(options.GraphApiBaseUrl, UriKind.Absolute, out var baseUri)
            || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("WhatsAppCloud:GraphApiBaseUrl must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.GraphApiVersion))
        {
            errors.Add("WhatsAppCloud:GraphApiVersion is required when WhatsAppCloud:Enabled is true.");
        }

        if (options.RequestTimeoutSeconds is < 1 or > 120)
        {
            errors.Add("WhatsAppCloud:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxRetryAttempts is < 0 or > 10)
        {
            errors.Add("WhatsAppCloud:MaxRetryAttempts must be between 0 and 10.");
        }

        if (options.MaxPostRetryAttempts is < 0 or > 2)
        {
            errors.Add("WhatsAppCloud:MaxPostRetryAttempts must be between 0 and 2.");
        }

        if (options.RetryBaseDelayMilliseconds is < 50 or > 5000)
        {
            errors.Add("WhatsAppCloud:RetryBaseDelayMilliseconds must be between 50 and 5000.");
        }

        if (options.RetryMaxDelaySeconds is < 1 or > 60)
        {
            errors.Add("WhatsAppCloud:RetryMaxDelaySeconds must be between 1 and 60.");
        }

        if (options.MaxTemplateSyncPages is < 1 or > 100)
        {
            errors.Add("WhatsAppCloud:MaxTemplateSyncPages must be between 1 and 100.");
        }

        if (options.MaxTemplateSyncItems is < 1 or > 5000)
        {
            errors.Add("WhatsAppCloud:MaxTemplateSyncItems must be between 1 and 5000.");
        }

        if (options.CustomerServiceWindowHours is < 1 or > 72)
        {
            errors.Add("WhatsAppCloud:CustomerServiceWindowHours must be between 1 and 72.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}