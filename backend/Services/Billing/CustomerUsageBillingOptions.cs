using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public sealed class CustomerUsageBillingOptions
{
    public const string SectionName = "Billing:UsageBilling";

    public bool Enabled { get; set; }
    public Dictionary<string, CustomerUsageBillingMetricOptions> Metrics { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class CustomerUsageBillingMetricOptions
{
    public string Transport { get; set; } = string.Empty;
    public string MeterEventName { get; set; } = string.Empty;
    public string CustomerPayloadKey { get; set; } = string.Empty;
    public string ValuePayloadKey { get; set; } = string.Empty;
    public bool BillableQuantityIsExternalUnit { get; set; }
}

public sealed class CustomerUsageBillingOptionsValidator : IValidateOptions<CustomerUsageBillingOptions>
{
    private static readonly Regex StripeKeyPattern = new("^[A-Za-z0-9_]{1,100}$", RegexOptions.CultureInvariant);

    public ValidateOptionsResult Validate(string? name, CustomerUsageBillingOptions options)
    {
        var errors = new List<string>();
        if (options.Metrics is null)
        {
            return ValidateOptionsResult.Fail("Billing:UsageBilling:Metrics must be an object.");
        }

        foreach (var (metricName, mapping) in options.Metrics)
        {
            if (!Enum.TryParse<UsageMetric>(metricName, ignoreCase: false, out var metric)
                || !Enum.IsDefined(metric)
                || !string.Equals(metricName, metric.ToString(), StringComparison.Ordinal))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName} is not an exact supported usage metric.");
                continue;
            }

            if (mapping is null)
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName} must contain a mapping.");
                continue;
            }

            if (!string.Equals(mapping.Transport, "StripeMeter", StringComparison.Ordinal))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:Transport must be StripeMeter.");
            }

            if (!string.Equals(mapping.Transport, "StripeMeter", StringComparison.Ordinal))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:Transport must be StripeMeter.");
            }

            if (!StripeKeyPattern.IsMatch(mapping.MeterEventName))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:MeterEventName must be 1 to 100 ASCII letters, digits, or underscores.");
            }

            if (!StripeKeyPattern.IsMatch(mapping.CustomerPayloadKey)
                || string.Equals(mapping.CustomerPayloadKey, mapping.ValuePayloadKey, StringComparison.Ordinal))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:CustomerPayloadKey must be valid and distinct from ValuePayloadKey.");
            }

            if (!StripeKeyPattern.IsMatch(mapping.ValuePayloadKey))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:ValuePayloadKey must be 1 to 100 ASCII letters, digits, or underscores.");
            }

            if (!mapping.BillableQuantityIsExternalUnit)
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:BillableQuantityIsExternalUnit must be true before usage can be sent.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    internal static bool IsValidMapping(CustomerUsageBillingMetricOptions mapping) =>
        string.Equals(mapping.Transport, "StripeMeter", StringComparison.Ordinal)
        && StripeKeyPattern.IsMatch(mapping.MeterEventName)
        && StripeKeyPattern.IsMatch(mapping.CustomerPayloadKey)
        && StripeKeyPattern.IsMatch(mapping.ValuePayloadKey)
        && !string.Equals(mapping.CustomerPayloadKey, mapping.ValuePayloadKey, StringComparison.Ordinal)
        && mapping.BillableQuantityIsExternalUnit;
}
