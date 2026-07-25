using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCloudOptions
{
    public const string SectionName = "WhatsAppCloud";

    public bool Enabled { get; set; }
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";
    public string GraphApiVersion { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string WebhookVerifyToken { get; set; } = string.Empty;
    public string DefaultCredentialReference { get; set; } = "default";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 2;
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

        if (string.IsNullOrWhiteSpace(options.GraphApiVersion))
        {
            errors.Add("WhatsAppCloud:GraphApiVersion is required when WhatsAppCloud:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.PhoneNumberId))
        {
            errors.Add("WhatsAppCloud:PhoneNumberId is required when WhatsAppCloud:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.WhatsAppBusinessAccountId))
        {
            errors.Add("WhatsAppCloud:WhatsAppBusinessAccountId is required when WhatsAppCloud:Enabled is true.");
        }

        if (!options.DevelopmentMode && string.IsNullOrWhiteSpace(options.AccessToken))
        {
            errors.Add("WhatsAppCloud:AccessToken is required when WhatsAppCloud:Enabled is true and development mode is disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.AppSecret))
        {
            errors.Add("WhatsAppCloud:AppSecret is required when WhatsAppCloud:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.WebhookVerifyToken))
        {
            errors.Add("WhatsAppCloud:WebhookVerifyToken is required when WhatsAppCloud:Enabled is true.");
        }

        if (options.RequestTimeoutSeconds is < 1 or > 120)
        {
            errors.Add("WhatsAppCloud:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxRetryAttempts is < 0 or > 10)
        {
            errors.Add("WhatsAppCloud:MaxRetryAttempts must be between 0 and 10.");
        }

        if (options.CustomerServiceWindowHours is < 1 or > 72)
        {
            errors.Add("WhatsAppCloud:CustomerServiceWindowHours must be between 1 and 72.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}