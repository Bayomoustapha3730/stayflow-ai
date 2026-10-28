using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class CustomerUsagePriceVersionConfiguration : IEntityTypeConfiguration<CustomerUsagePriceVersion>
{
    public void Configure(EntityTypeBuilder<CustomerUsagePriceVersion> builder)
    {
        builder.ToTable("CustomerUsagePriceVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_CustomerUsagePriceVersions_Identity",
                "\"SubscriptionPlanId\" <> '00000000-0000-0000-0000-000000000000' AND \"Metric\" IN ('Users', 'Properties', 'Reservations', 'AiRequests', 'AiTokens', 'WhatsAppMessages', 'ApiRequests', 'StorageBytes', 'FileUploads')");
            table.HasCheckConstraint(
                "CK_CustomerUsagePriceVersions_BillingMode",
                "\"BillingMode\" IN ('IncludedOnly', 'PerUnitOverage', 'NotBillable')");
            table.HasCheckConstraint(
                "CK_CustomerUsagePriceVersions_EffectivePeriod",
                "\"EffectiveToUtc\" IS NULL OR \"EffectiveToUtc\" > \"EffectiveFromUtc\"");
            table.HasCheckConstraint(
                "CK_CustomerUsagePriceVersions_Currency",
                "\"Currency\" = 'USD'");
            table.HasCheckConstraint(
                "CK_CustomerUsagePriceVersions_UnitQuantity",
                "\"UnitQuantity\" > 0");
            table.HasCheckConstraint(
                "CK_CustomerUsagePriceVersions_UnitPrice",
                "\"UnitPrice\" >= 0");
        });

        builder.HasKey(version => version.Id);
        builder.Property(version => version.Metric).HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(version => version.BillingMode)
            .HasConversion<CustomerUsageBillingModeStorageConverter>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(version => version.UnitQuantity).HasPrecision(28, 12).IsRequired();
        builder.Property(version => version.UnitPrice).HasPrecision(28, 12).IsRequired();
        builder.Property(version => version.Currency).HasMaxLength(3).IsRequired();
        builder.Property(version => version.EffectiveFromUtc).IsRequired();

        builder.HasOne(version => version.SubscriptionPlan)
            .WithMany(plan => plan.CustomerUsagePriceVersions)
            .HasForeignKey(version => version.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(version => new
        {
            version.SubscriptionPlanId,
            version.Metric,
            version.EffectiveFromUtc
        });
    }
}
