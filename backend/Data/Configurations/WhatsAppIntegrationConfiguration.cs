using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class WhatsAppIntegrationConfiguration : IEntityTypeConfiguration<WhatsAppIntegration>
{
    public void Configure(EntityTypeBuilder<WhatsAppIntegration> builder)
    {
        builder.ToTable("WhatsAppIntegrations");

        builder.HasKey(integration => integration.Id);

        builder.Property(integration => integration.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(integration => integration.PhoneNumberId).HasMaxLength(160).IsRequired();
        builder.Property(integration => integration.WhatsAppBusinessAccountId).HasMaxLength(160).IsRequired();
        builder.Property(integration => integration.BusinessPhoneNumberMasked).HasMaxLength(32).IsRequired();
        builder.Property(integration => integration.GraphApiVersion).HasMaxLength(40).IsRequired();
        builder.Property(integration => integration.CredentialReference).HasMaxLength(120);
        builder.Property(integration => integration.WebhookConfigurationStatus).HasMaxLength(60).IsRequired();
        builder.Property(integration => integration.TemplateSyncStatus).HasMaxLength(60).IsRequired();
        builder.Property(integration => integration.LastErrorSummary).HasMaxLength(280);
        builder.Property(integration => integration.IsDemoSeeded).IsRequired().HasDefaultValue(false);

        builder.HasOne(integration => integration.Company)
            .WithMany(company => company.WhatsAppIntegrations)
            .HasForeignKey(integration => integration.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(integration => integration.CompanyId);
        builder.HasIndex(integration => integration.IsActive);
        builder.HasIndex(integration => integration.PhoneNumberId).IsUnique();
        builder.HasIndex(integration => new { integration.CompanyId, integration.IsActive });
        builder.HasIndex(integration => new { integration.CompanyId, integration.IsProductionEnabled });
    }
}