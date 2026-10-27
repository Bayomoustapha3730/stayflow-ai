using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

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
        });

        builder.HasKey(call => call.Id);
        builder.Property(call => call.ProviderCallId).HasMaxLength(64).IsRequired();
        builder.Property(call => call.OuterOperationType).HasConversion<string>().HasMaxLength(40);
        builder.Property(call => call.Provider).HasMaxLength(80);
        builder.Property(call => call.Model).HasMaxLength(120);
        builder.Property(call => call.Outcome).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(call => call.Company)
            .WithMany()
            .HasForeignKey(call => call.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(call => call.ProviderCallId)
            .IsUnique()
            .HasDatabaseName(ProviderCallIdUniqueIndexName);
        builder.HasIndex(call => new { call.CompanyId, call.OuterOperationType, call.OuterOperationId });
        builder.HasIndex(call => call.CreatedAt);
    }
}
