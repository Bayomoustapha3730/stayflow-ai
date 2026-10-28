using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class CustomerBillableUsageOperationConfiguration : IEntityTypeConfiguration<CustomerBillableUsageOperation>
{
    public void Configure(EntityTypeBuilder<CustomerBillableUsageOperation> builder)
    {
        builder.ToTable("CustomerBillableUsageOperations", table =>
        {
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_Identity",
                "\"CompanyId\" <> '00000000-0000-0000-0000-000000000000' AND \"TenantSubscriptionId\" <> '00000000-0000-0000-0000-000000000000' AND \"SubscriptionPlanId\" <> '00000000-0000-0000-0000-000000000000' AND \"Metric\" IN ('Users', 'Properties', 'Reservations', 'AiRequests', 'AiTokens', 'WhatsAppMessages', 'ApiRequests', 'StorageBytes', 'FileUploads')");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_Quantities",
                "\"ConsumedQuantity\" > 0 AND \"UsageBefore\" >= 0 AND \"UsageAfter\" = \"UsageBefore\" + \"ConsumedQuantity\" AND (\"IncludedQuantity\" IS NULL OR \"IncludedQuantity\" >= 0)");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_BillableQuantity",
                "\"BillableQuantity\" >= 0 AND \"BillableQuantity\" <= \"ConsumedQuantity\"");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_Intervals",
                "\"PeriodEndUtc\" > \"PeriodStartUtc\" AND \"OccurredAtUtc\" >= \"PeriodStartUtc\" AND \"OccurredAtUtc\" <= \"PeriodEndUtc\"");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_UnitPrice",
                "(\"UnitQuantity\" IS NULL AND \"UnitPrice\" IS NULL) OR (\"UnitQuantity\" IS NOT NULL AND \"UnitQuantity\" > 0 AND \"UnitPrice\" IS NOT NULL AND \"UnitPrice\" >= 0)");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_ChargeAmount",
                "\"ChargeAmount\" IS NULL OR \"ChargeAmount\" >= 0");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_Currency",
                "\"Currency\" IS NULL OR \"Currency\" = 'USD'");
            table.HasCheckConstraint(
                "CK_CustomerBillableUsageOperations_BillingState",
                "(\"BillingState\" = 'Calculated' AND \"BillingReason\" = 'Calculated' AND \"BillableQuantity\" > 0 AND \"CustomerUsagePriceVersionId\" IS NOT NULL AND \"UnitQuantity\" IS NOT NULL AND \"UnitQuantity\" > 0 AND \"UnitPrice\" IS NOT NULL AND \"UnitPrice\" >= 0 AND \"ChargeAmount\" IS NOT NULL AND \"ChargeAmount\" >= 0 AND \"Currency\" = 'USD') OR (\"BillingState\" = 'NotBillable' AND \"BillingReason\" IN ('WithinIncludedAllowance', 'IncludedOnly', 'MetricNotBillable') AND \"ChargeAmount\" IS NULL AND \"Currency\" IS NULL) OR (\"BillingState\" = 'Unknown' AND \"BillingReason\" IN ('MissingPrice', 'AmbiguousPrice', 'UnsupportedBillingMode', 'MissingEntitlement') AND \"BillableQuantity\" > 0 AND \"ChargeAmount\" IS NULL AND \"Currency\" IS NULL)");
        });

        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Metric).HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(operation => operation.BillingState).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(operation => operation.BillingReason).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(operation => operation.UnitQuantity).HasPrecision(28, 12);
        builder.Property(operation => operation.UnitPrice).HasPrecision(28, 12);
        builder.Property(operation => operation.ChargeAmount).HasPrecision(28, 18);
        builder.Property(operation => operation.Currency).HasMaxLength(3);

        builder.HasOne(operation => operation.UsageOperation)
            .WithOne()
            .HasForeignKey<CustomerBillableUsageOperation>(operation => operation.UsageOperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantSubscription>()
            .WithMany()
            .HasForeignKey(operation => operation.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(operation => operation.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerUsagePriceVersion>()
            .WithMany()
            .HasForeignKey(operation => operation.CustomerUsagePriceVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>()
            .WithMany(company => company.CustomerBillableUsageOperations)
            .HasForeignKey(operation => operation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(operation => operation.UsageOperationId)
            .IsUnique()
            .HasDatabaseName("UX_CustomerBillableUsageOperations_UsageOperationId");
        builder.HasIndex(operation => new { operation.CompanyId, operation.PeriodStartUtc, operation.Metric });
        builder.HasIndex(operation => operation.TenantSubscriptionId);
        builder.HasIndex(operation => operation.SubscriptionPlanId);
        builder.HasIndex(operation => operation.CustomerUsagePriceVersionId);
    }
}
