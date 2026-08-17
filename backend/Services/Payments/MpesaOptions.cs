using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.Payments;

/// <summary>
/// Safaricom Daraja (M-PESA) configuration. Secrets are never stored here; only
/// credential references that resolve to environment variables at runtime.
/// </summary>
public sealed class MpesaOptions
{
    public const string SectionName = "Mpesa";

    public bool Enabled { get; set; }

    /// <summary>Sandbox or Production.</summary>
    public string Environment { get; set; } = "Sandbox";

    public string BaseUrl { get; set; } = "https://sandbox.safaricom.co.ke";

    /// <summary>Logical credential name resolved to STAYFLOW_MPESA_&lt;REFERENCE&gt;_* variables.</summary>
    public string DefaultCredentialReference { get; set; } = "default";

    /// <summary>Business short code (Paybill/Till). Not a secret.</summary>
    public string ShortCode { get; set; } = string.Empty;

    public string TransactionType { get; set; } = "CustomerPayBillOnline";

    /// <summary>Public HTTPS base used to build the Safaricom callback URL.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int MaxRetryAttempts { get; set; } = 2;

    public int RetryBaseDelayMilliseconds { get; set; } = 250;

    /// <summary>Seconds subtracted from the token lifetime so cached tokens are refreshed early.</summary>
    public int TokenExpirySkewSeconds { get; set; } = 60;

    /// <summary>Uses an in-process fake Daraja client. Only permitted in Development.</summary>
    public bool DevelopmentMode { get; set; }

    public bool IsProduction =>
        string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase);
}

public sealed class MpesaOptionsValidator : IValidateOptions<MpesaOptions>
{
    private static readonly string[] AllowedEnvironments = ["Sandbox", "Production"];

    private static readonly string[] AllowedTransactionTypes =
        ["CustomerPayBillOnline", "CustomerBuyGoodsOnline"];

    public ValidateOptionsResult Validate(string? name, MpesaOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (!AllowedEnvironments.Contains(options.Environment, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Mpesa:Environment must be either 'Sandbox' or 'Production'.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
            || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Mpesa:BaseUrl must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ShortCode))
        {
            errors.Add("Mpesa:ShortCode is required when Mpesa:Enabled is true.");
        }
        else if (!options.ShortCode.All(char.IsAsciiDigit))
        {
            errors.Add("Mpesa:ShortCode must contain digits only.");
        }

        if (!AllowedTransactionTypes.Contains(options.TransactionType, StringComparer.Ordinal))
        {
            errors.Add(
                "Mpesa:TransactionType must be 'CustomerPayBillOnline' or 'CustomerBuyGoodsOnline'.");
        }

        if (string.IsNullOrWhiteSpace(options.CallbackBaseUrl))
        {
            errors.Add("Mpesa:CallbackBaseUrl is required when Mpesa:Enabled is true.");
        }
        else if (!Uri.TryCreate(options.CallbackBaseUrl, UriKind.Absolute, out var callbackUri)
            || !string.Equals(callbackUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Mpesa:CallbackBaseUrl must be an absolute HTTPS URL.");
        }

        if (options.RequestTimeoutSeconds is < 1 or > 120)
        {
            errors.Add("Mpesa:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxRetryAttempts is < 0 or > 5)
        {
            errors.Add("Mpesa:MaxRetryAttempts must be between 0 and 5.");
        }

        if (options.DevelopmentMode && options.IsProduction)
        {
            errors.Add("Mpesa:DevelopmentMode cannot be enabled when Mpesa:Environment is 'Production'.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
