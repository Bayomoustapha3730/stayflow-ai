using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class CustomerBillingDeliveryConfiguration : IEntityTypeConfiguration<CustomerBillingDelivery>
{
    public void Configure(EntityTypeBuilder<CustomerBillingDelivery> builder)
    {
        builder.ToTable("CustomerBillingDeliveries", table =>
        {
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_Identity",
                "\"CompanyId\" <> '00000000-0000-0000-0000-000000000000' AND \"CustomerBillableUsageOperationId\" <> '00000000-0000-0000-0000-000000000000' AND \"TenantSubscriptionId\" <> '00000000-0000-0000-0000-000000000000' AND \"SubscriptionPlanId\" <> '00000000-0000-0000-0000-000000000000' AND \"CustomerUsagePriceVersionId\" <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_BoundedValues",
                "\"DeliveryProvider\" = 'Stripe' AND \"DeliveryKind\" = 'PendingExternalBilling' AND \"DeliveryState\" IN ('Prepared', 'Dispatching', 'Delivered', 'Failed', 'Indeterminate', 'Suppressed') AND \"DeliveryReason\" IN ('Prepared', 'DispatchClaimed', 'Delivered', 'ZeroChargeSuppressed', 'ProviderRejected', 'ProviderUnavailable', 'Timeout', 'Canceled', 'NetworkFailure', 'PersistenceFailure', 'IndeterminateExternalOutcome', 'ConfigurationMissing', 'InvalidSubscriptionMapping', 'InvalidPriceMapping') AND \"BillingStateSnapshot\" = 'Calculated' AND \"BillingReasonSnapshot\" = 'Calculated' AND \"Metric\" IN ('Users', 'Properties', 'Reservations', 'AiRequests', 'AiTokens', 'WhatsAppMessages', 'ApiRequests', 'StorageBytes', 'FileUploads')");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_FinancialFacts",
                "\"BillableQuantitySnapshot\" >= 0 AND \"ChargeAmountSnapshot\" >= 0 AND \"CurrencySnapshot\" = 'USD'");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_Period",
                "\"PeriodStartUtc\" < \"PeriodEndUtc\"");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_AttemptCount",
                "\"AttemptCount\" >= 0");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_IdempotencyKey",
                "length(\"ExternalIdempotencyKey\") > 0");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_FailureCategory",
                "\"LastFailureCategory\" IS NULL OR \"LastFailureCategory\" IN ('ProviderRejected', 'ProviderUnavailable', 'Timeout', 'Canceled', 'NetworkFailure', 'PersistenceFailure', 'ConfigurationMissing', 'InvalidSubscriptionMapping', 'InvalidPriceMapping', 'RetryableProviderUnavailable')");
            table.HasCheckConstraint(
                "CK_CustomerBillingDeliveries_DeliveryTimestamps",
                "(\"DeliveryState\" = 'Delivered' AND \"DeliveredAtUtc\" IS NOT NULL) OR (\"DeliveryState\" <> 'Delivered' AND \"DeliveredAtUtc\" IS NULL)");
        });

        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Metric).HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(delivery => delivery.DeliveryProvider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.DeliveryKind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(delivery => delivery.BillingStateSnapshot).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.BillingReasonSnapshot).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(delivery => delivery.DeliveryState).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.DeliveryReason).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(delivery => delivery.CurrencySnapshot).HasMaxLength(3).IsRequired();
        builder.Property(delivery => delivery.ChargeAmountSnapshot).HasPrecision(28, 18);
        builder.Property(delivery => delivery.ExternalIdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(delivery => delivery.ExternalObjectId).HasMaxLength(200);
        builder.Property(delivery => delivery.ExternalRequestId).HasMaxLength(200);
        builder.Property(delivery => delivery.LastFailureCategory).HasConversion<string>().HasMaxLength(40);
        builder.Property(delivery => delivery.LastFailureCode).HasMaxLength(80);

        builder.HasOne<CustomerBillableUsageOperation>()
            .WithMany()
            .HasForeignKey(delivery => delivery.CustomerBillableUsageOperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(delivery => delivery.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantSubscription>()
            .WithMany()
            .HasForeignKey(delivery => delivery.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(delivery => delivery.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerUsagePriceVersion>()
            .WithMany()
            .HasForeignKey(delivery => delivery.CustomerUsagePriceVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(delivery => new
            {
                delivery.CustomerBillableUsageOperationId,
                delivery.DeliveryProvider,
                delivery.DeliveryKind
            })
            .IsUnique()
            .HasDatabaseName("UX_CustomerBillingDeliveries_BillingOperationProviderKind");
        builder.HasIndex(delivery => delivery.ExternalIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_CustomerBillingDeliveries_ExternalIdempotencyKey");
        builder.HasIndex(delivery => new { delivery.DeliveryState, delivery.CreatedAtUtc });
    }
}
