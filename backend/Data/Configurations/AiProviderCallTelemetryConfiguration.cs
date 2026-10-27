using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Data.Configurations;

public sealed class AiProviderCallTelemetryConfiguration : IEntityTypeConfiguration<AiProviderCallTelemetry>
{
    public const string ProviderCallIdUniqueIndexName = "UX_AiProviderCallTelemetries_ProviderCallId";

    public void Configure(EntityTypeBuilder<AiProviderCallTelemetry> builder)
    {
        builder.ToTable("AiProviderCallTelemetries", table =>
        {
            table.HasCheckConstraint(
                "CK_AiProviderCallTelemetries_Usage",
                "(\"UsageKnown\" = FALSE AND \"InputTokens\" IS NULL AND \"OutputTokens\" IS NULL AND \"TotalTokens\" IS NULL) OR (\"UsageKnown\" = TRUE AND \"InputTokens\" IS NOT NULL AND \"OutputTokens\" IS NOT NULL AND \"TotalTokens\" IS NOT NULL AND \"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"TotalTokens\" >= 0)");
            table.HasCheckConstraint(
                "CK_AiProviderCallTelemetries_OuterOperation",
                "(\"OuterOperationType\" IS NULL AND \"OuterOperationId\" IS NULL) OR (\"OuterOperationType\" IS NOT NULL AND \"OuterOperationId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_AiProviderCallTelemetries_CostAmount",
                "\"ProviderCostAmount\" IS NULL OR \"ProviderCostAmount\" >= 0");
            table.HasCheckConstraint(
                "CK_AiProviderCallTelemetries_CostCurrency",
                "\"ProviderCostCurrency\" IS NULL OR \"ProviderCostCurrency\" = 'USD'");
            table.HasCheckConstraint(
                "CK_AiProviderCallTelemetries_CostReason",
                "\"CostReason\" IN ('Calculated', 'MissingResolvedModel', 'MissingDispatchTimestamp', 'UnknownProvider', 'UnknownModel', 'NoEffectivePrice', 'AmbiguousPrice', 'MissingTokenUsage', 'UnsupportedPricingDimension', 'ProviderUsageUnavailable', 'NonBillableProvider', 'CalculationOverflow')");
            table.HasCheckConstraint(
                "CK_AiProviderCallTelemetries_CostState",
                "(\"CostState\" = 'Calculated' AND \"CostReason\" = 'Calculated' AND \"PricingVersionId\" IS NOT NULL AND \"ProviderCostAmount\" IS NOT NULL AND \"ProviderCostCurrency\" = 'USD') OR (\"CostState\" = 'Unknown' AND \"CostReason\" NOT IN ('Calculated', 'NonBillableProvider') AND \"ProviderCostAmount\" IS NULL AND \"ProviderCostCurrency\" IS NULL) OR (\"CostState\" = 'NotApplicable' AND \"CostReason\" = 'NonBillableProvider' AND \"PricingVersionId\" IS NULL AND \"ProviderCostAmount\" IS NULL AND \"ProviderCostCurrency\" IS NULL)");
        });

        builder.HasKey(call => call.Id);
        builder.Property(call => call.ProviderCallId).HasMaxLength(64).IsRequired();
        builder.Property(call => call.OuterOperationType).HasConversion<string>().HasMaxLength(40);
        builder.Property(call => call.Provider).HasMaxLength(80);
        builder.Property(call => call.RequestedModel).HasMaxLength(120);
        builder.Property(call => call.ResolvedModel).HasMaxLength(120);
        builder.Property(call => call.ProviderRequestId).HasMaxLength(160);
        builder.Property(call => call.LifecycleState).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(call => call.FailureCategory).HasMaxLength(80);
        builder.Property(call => call.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(call => call.CostState).HasConversion<string>().HasMaxLength(20).HasDefaultValue(AiProviderCostState.Unknown);
        builder.Property(call => call.CostReason).HasConversion<string>().HasMaxLength(40).HasDefaultValue(AiProviderCostReason.ProviderUsageUnavailable);
        builder.Property(call => call.ProviderCostAmount).HasPrecision(28, 18);
        builder.Property(call => call.ProviderCostCurrency).HasMaxLength(3);

        builder.HasOne(call => call.Company)
            .WithMany()
            .HasForeignKey(call => call.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(call => call.PricingVersion)
            .WithMany()
            .HasForeignKey(call => call.PricingVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(call => call.ProviderCallId)
            .IsUnique()
            .HasDatabaseName(ProviderCallIdUniqueIndexName);
        builder.HasIndex(call => new { call.CompanyId, call.OuterOperationType, call.OuterOperationId });
        builder.HasIndex(call => call.CreatedAt);
        builder.HasIndex(call => call.PricingVersionId);
    }
}
