using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed record CustomerUsageChargeCalculation(
    long UsageAfter,
    long BillableQuantity,
    decimal? ChargeAmount);

public sealed record CustomerUsageBillingCalculation(
    CustomerBillableUsageState State,
    CustomerBillableUsageReason Reason,
    Guid? PricingVersionId = null,
    decimal? UnitQuantity = null,
    decimal? UnitPrice = null,
    decimal? ChargeAmount = null,
    string? Currency = null);

public static class CustomerUsageChargeCalculator
{
    public static CustomerUsageChargeCalculation Calculate(
        long usageBefore,
        long consumedQuantity,
        long? includedQuantity,
        decimal? unitQuantity = null,
        decimal? unitPrice = null)
    {
        if (usageBefore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usageBefore));
        }

        if (consumedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(consumedQuantity));
        }

        if (includedQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(includedQuantity));
        }

        var usageAfter = checked(usageBefore + consumedQuantity);
        if (includedQuantity is null)
        {
            return new CustomerUsageChargeCalculation(usageAfter, 0, null);
        }

        var billableBefore = Math.Max(0, usageBefore - includedQuantity.Value);
        var billableAfter = Math.Max(0, usageAfter - includedQuantity.Value);
        var billableQuantity = checked(billableAfter - billableBefore);
        if (billableQuantity == 0 || unitQuantity is null || unitPrice is null)
        {
            return new CustomerUsageChargeCalculation(usageAfter, billableQuantity, null);
        }

        if (unitQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitQuantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        var chargeAmount = checked((decimal)billableQuantity * unitPrice.Value / unitQuantity.Value);
        return new CustomerUsageChargeCalculation(usageAfter, billableQuantity, chargeAmount);
    }

    public static CustomerUsageBillingCalculation ResolvePrice(
        long billableQuantity,
        CustomerUsagePricingResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (billableQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(billableQuantity));
        }

        if (resolution.State == CustomerUsagePricingResolutionState.NotBillable
            && resolution.Reason == CustomerUsagePricingResolutionReason.IncludedOnly)
        {
            return new CustomerUsageBillingCalculation(
                CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.IncludedOnly,
                resolution.PricingVersionId);
        }

        if (resolution.State == CustomerUsagePricingResolutionState.NotBillable
            && resolution.Reason == CustomerUsagePricingResolutionReason.MetricNotBillable)
        {
            return new CustomerUsageBillingCalculation(
                CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.MetricNotBillable,
                resolution.PricingVersionId);
        }

        if (resolution.State == CustomerUsagePricingResolutionState.Resolved
            && resolution.Reason == CustomerUsagePricingResolutionReason.Resolved
            && resolution.PricingVersionId is not null
            && resolution.UnitQuantity is > 0
            && resolution.UnitPrice is >= 0
            && resolution.Currency == "USD")
        {
            var chargeAmount = checked(
                (decimal)billableQuantity * resolution.UnitPrice.Value / resolution.UnitQuantity.Value);
            return new CustomerUsageBillingCalculation(
                CustomerBillableUsageState.Calculated,
                CustomerBillableUsageReason.Calculated,
                resolution.PricingVersionId,
                resolution.UnitQuantity,
                resolution.UnitPrice,
                chargeAmount,
                resolution.Currency);
        }

        var reason = resolution.State == CustomerUsagePricingResolutionState.Unknown
            ? resolution.Reason switch
            {
                CustomerUsagePricingResolutionReason.MissingPrice => CustomerBillableUsageReason.MissingPrice,
                CustomerUsagePricingResolutionReason.AmbiguousPrice => CustomerBillableUsageReason.AmbiguousPrice,
                _ => CustomerBillableUsageReason.UnsupportedBillingMode
            }
            : CustomerBillableUsageReason.UnsupportedBillingMode;
        return new CustomerUsageBillingCalculation(
            CustomerBillableUsageState.Unknown,
            reason);
    }
}
