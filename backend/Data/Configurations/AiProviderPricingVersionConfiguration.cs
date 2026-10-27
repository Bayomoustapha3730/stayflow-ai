using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class AiProviderPricingVersionConfiguration : IEntityTypeConfiguration<AiProviderPricingVersion>
{
    public void Configure(EntityTypeBuilder<AiProviderPricingVersion> builder)
    {
        builder.ToTable("AiProviderPricingVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_AiProviderPricingVersions_Identity",
                "length(\"Provider\") > 0 AND length(\"PricingModelKey\") > 0");
            table.HasCheckConstraint(
                "CK_AiProviderPricingVersions_EffectivePeriod",
                "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
            table.HasCheckConstraint(
                "CK_AiProviderPricingVersions_Currency",
                "\"Currency\" = 'USD'");
            table.HasCheckConstraint(
                "CK_AiProviderPricingVersions_Prices",
                "\"InputPrice\" >= 0 AND \"OutputPrice\" >= 0");
            table.HasCheckConstraint(
                "CK_AiProviderPricingVersions_UnitTokenCount",
                "\"UnitTokenCount\" > 0");
        });

        builder.HasKey(version => version.Id);
        builder.Property(version => version.Provider).HasMaxLength(80).IsRequired();
        builder.Property(version => version.PricingModelKey).HasMaxLength(120).IsRequired();
        builder.Property(version => version.EffectiveFrom).IsRequired();
        builder.Property(version => version.Currency).HasMaxLength(3).IsRequired();
        builder.Property(version => version.InputPrice).HasPrecision(28, 12).IsRequired();
        builder.Property(version => version.OutputPrice).HasPrecision(28, 12).IsRequired();
        builder.Property(version => version.UnitTokenCount).IsRequired();

        builder.HasIndex(version => new { version.Provider, version.PricingModelKey, version.EffectiveFrom });
    }
}
