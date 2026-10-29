using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public sealed class CustomerUsageBillingOptions
{
    public const string SectionName = "Billing:UsageBilling";

    public bool Enabled { get; set; }
    public bool OpenPeriodInvoiceSafetyVerified { get; set; }
    public Dictionary<string, CustomerUsageBillingMetricOptions> Metrics { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class CustomerUsageBillingMetricOptions
{
    public string Transport { get; set; } = string.Empty;
    public string CustomerPayloadKey { get; set; } = "stripe_customer_id";
    public string ValuePayloadKey { get; set; } = "value";
    public bool BillableQuantityIsExternalUnit { get; set; }
    public Dictionary<string, CustomerUsageBillingPriceMappingOptions> PriceMappings { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class CustomerUsageBillingPriceMappingOptions
{
    public bool ResourcesVerified { get; set; }
    public string MeterId { get; set; } = string.Empty;
    public string MeterEventName { get; set; } = string.Empty;
    public string UsagePriceId { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal UnitQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal StripeUnitAmountMinor { get; set; }
    public string RateSemantics { get; set; } = string.Empty;
    public string MappingIdentity { get; set; } = string.Empty;
}

public sealed class CustomerUsageBillingOptionsValidator : IValidateOptions<CustomerUsageBillingOptions>
{
    private static readonly Regex StripeKeyPattern = new("^[A-Za-z0-9_]{1,100}$", RegexOptions.CultureInvariant);
    private static readonly Regex StripeIdPattern = new("^(mtr|price)_[A-Za-z0-9]+$", RegexOptions.CultureInvariant);
    private static readonly Regex StripeMeterIdPattern = new(@"\Amtr_(?:test_)?[A-Za-z0-9]+\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<UsageMetric> ExternallySupportedMetrics =
    [
        UsageMetric.Reservations,
        UsageMetric.AiRequests,
        UsageMetric.WhatsAppMessages
    ];

    public ValidateOptionsResult Validate(string? name, CustomerUsageBillingOptions options)
    {
        var errors = new List<string>();
        if (options.Metrics is null)
        {
            return ValidateOptionsResult.Fail("Billing:UsageBilling:Metrics must be an object.");
        }

        var eventNames = new HashSet<string>(StringComparer.Ordinal);
        var meterIds = new HashSet<string>(StringComparer.Ordinal);
        var priceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (metricName, metricOptions) in options.Metrics)
        {
            if (!Enum.TryParse<UsageMetric>(metricName, ignoreCase: false, out var metric)
                || !Enum.IsDefined(metric)
                || !string.Equals(metricName, metric.ToString(), StringComparison.Ordinal))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName} is not an exact supported usage metric.");
                continue;
            }

            if (!ExternallySupportedMetrics.Contains(metric))
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName} has no approved external unit contract.");
                continue;
            }

            if (metricOptions is null)
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName} must contain a mapping.");
                continue;
            }

            ValidateMetricMapping(metricName, metricOptions, errors);
            if (metricOptions.PriceMappings is null)
            {
                errors.Add($"Billing:UsageBilling:Metrics:{metricName}:PriceMappings must be an object.");
                continue;
            }

            foreach (var (identity, priceMapping) in metricOptions.PriceMappings)
            {
                if (!TryParseIdentity(identity, out var planId, out var priceVersionId))
                {
                    errors.Add($"Billing:UsageBilling:Metrics:{metricName}:PriceMappings:{identity} must be a canonical planId/priceVersionId pair.");
                    continue;
                }

                if (priceMapping is null)
                {
                    errors.Add($"Billing:UsageBilling:Metrics:{metricName}:PriceMappings:{identity} must contain a mapping.");
                    continue;
                }

                ValidatePriceMapping(metricName, identity, planId, priceVersionId, priceMapping, errors);
                if (!string.IsNullOrWhiteSpace(priceMapping.MeterEventName)
                    && !eventNames.Add(priceMapping.MeterEventName))
                {
                    errors.Add($"Billing:UsageBilling:Metrics:{metricName}:PriceMappings:{identity}:MeterEventName must be unique.");
                }
                if (!meterIds.Add(priceMapping.MeterId) || !priceIds.Add(priceMapping.UsagePriceId))
                {
                    errors.Add($"Billing:UsageBilling:Metrics:{metricName}:PriceMappings:{identity} must use a distinct meter and price for each immutable route.");
                }
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    internal static bool IsValidMetricMapping(UsageMetric metric, CustomerUsageBillingMetricOptions mapping) =>
        ExternallySupportedMetrics.Contains(metric)
        && string.Equals(mapping.Transport, "StripeMeter", StringComparison.Ordinal)
        && StripeKeyPattern.IsMatch(mapping.CustomerPayloadKey ?? string.Empty)
        && StripeKeyPattern.IsMatch(mapping.ValuePayloadKey ?? string.Empty)
        && !string.Equals(mapping.CustomerPayloadKey, mapping.ValuePayloadKey, StringComparison.Ordinal)
        && mapping.BillableQuantityIsExternalUnit
        && mapping.PriceMappings is not null;

    internal static bool TryParseIdentity(string? identity, out Guid planId, out Guid priceVersionId)
    {
        planId = Guid.Empty;
        priceVersionId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(identity))
        {
            return false;
        }

        var parts = identity.Split('/');
        return parts.Length == 2
            && Guid.TryParseExact(parts[0], "D", out planId)
            && Guid.TryParseExact(parts[1], "D", out priceVersionId)
            && planId != Guid.Empty
            && priceVersionId != Guid.Empty
            && identity == PriceMappingKey(planId, priceVersionId);
    }

    internal static string PriceMappingKey(Guid planId, Guid priceVersionId) =>
        $"{planId:D}/{priceVersionId:D}";

    internal static string ExpectedMappingIdentity(
        Guid planId,
        UsageMetric metric,
        Guid priceVersionId,
        string meterId,
        string meterEventName,
        string usagePriceId) =>
        $"{planId:D}:{metric}:{priceVersionId:D}:{meterId}:{meterEventName}:{usagePriceId}";

    private static void ValidateMetricMapping(
        string metricName,
        CustomerUsageBillingMetricOptions mapping,
        ICollection<string> errors)
    {
        if (!string.Equals(mapping.Transport, "StripeMeter", StringComparison.Ordinal))
        {
            errors.Add($"Billing:UsageBilling:Metrics:{metricName}:Transport must be StripeMeter.");
        }

        if (!StripeKeyPattern.IsMatch(mapping.CustomerPayloadKey ?? string.Empty)
            || string.Equals(mapping.CustomerPayloadKey, mapping.ValuePayloadKey, StringComparison.Ordinal))
        {
            errors.Add($"Billing:UsageBilling:Metrics:{metricName}:CustomerPayloadKey must be valid and distinct from ValuePayloadKey.");
        }

        if (!StripeKeyPattern.IsMatch(mapping.ValuePayloadKey ?? string.Empty))
        {
            errors.Add($"Billing:UsageBilling:Metrics:{metricName}:ValuePayloadKey must be valid.");
        }

        if (!mapping.BillableQuantityIsExternalUnit)
        {
            errors.Add($"Billing:UsageBilling:Metrics:{metricName}:BillableQuantityIsExternalUnit must be true before usage can be sent.");
        }
    }

    private static void ValidatePriceMapping(
        string metricName,
        string identity,
        Guid planId,
        Guid priceVersionId,
        CustomerUsageBillingPriceMappingOptions mapping,
        ICollection<string> errors)
    {
        var prefix = $"Billing:UsageBilling:Metrics:{metricName}:PriceMappings:{identity}";
        var meterId = mapping.MeterId ?? string.Empty;
        var meterEventName = mapping.MeterEventName ?? string.Empty;
        var usagePriceId = mapping.UsagePriceId ?? string.Empty;
        if (!mapping.ResourcesVerified)
        {
            errors.Add($"{prefix}:ResourcesVerified requires operational verification of the external meter, price, subscription item, and immutable rate contract.");
        }
        if (!StripeMeterIdPattern.IsMatch(meterId))
        {
            errors.Add($"{prefix}:MeterId must be a Stripe meter ID.");
        }

        if (!StripeKeyPattern.IsMatch(meterEventName))
        {
            errors.Add($"{prefix}:MeterEventName must be 1 to 100 ASCII letters, digits, or underscores.");
        }

        if (!StripeIdPattern.IsMatch(usagePriceId)
            || !usagePriceId.StartsWith("price_", StringComparison.Ordinal))
        {
            errors.Add($"{prefix}:UsagePriceId must be a Stripe Price ID.");
        }

        if (!string.Equals(mapping.Currency, "USD", StringComparison.Ordinal))
        {
            errors.Add($"{prefix}:Currency must be USD.");
        }

        if (mapping.UnitQuantity <= 0m || mapping.UnitPrice < 0m)
        {
            errors.Add($"{prefix}:UnitQuantity must be positive and UnitPrice cannot be negative.");
        }

        if (mapping.UnitQuantity > 0m && mapping.UnitPrice >= 0m)
        {
            try
            {
                var rate = mapping.UnitPrice / mapping.UnitQuantity;
                var expectedMinorRate = decimal.Round(rate * 100m, 12, MidpointRounding.ToEven);
                if (mapping.StripeUnitAmountMinor != expectedMinorRate || expectedMinorRate / 100m != rate)
                {
                    errors.Add($"{prefix}:StripeUnitAmountMinor must exactly match the persisted USD rate basis.");
                }
            }
            catch (OverflowException)
            {
                errors.Add($"{prefix}:StripeUnitAmountMinor rate is outside supported decimal precision.");
            }
        }

        if (!string.Equals(mapping.RateSemantics, "LinearPerBillableUnit", StringComparison.Ordinal))
        {
            errors.Add($"{prefix}:RateSemantics must be LinearPerBillableUnit.");
        }

        var metric = Enum.Parse<UsageMetric>(metricName, ignoreCase: false);
        var expectedIdentity = ExpectedMappingIdentity(
            planId,
            metric,
            priceVersionId,
            meterId,
            meterEventName,
            usagePriceId);
        if (!string.Equals(mapping.MappingIdentity, expectedIdentity, StringComparison.Ordinal))
        {
            errors.Add($"{prefix}:MappingIdentity must exactly identify plan, metric, price version, and usage Price.");
        }
    }
}
