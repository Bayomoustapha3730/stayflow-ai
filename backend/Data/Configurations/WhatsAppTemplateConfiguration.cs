using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class WhatsAppTemplateConfiguration : IEntityTypeConfiguration<WhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplate> builder)
    {
        builder.ToTable("WhatsAppTemplates");

        builder.HasKey(template => template.Id);

        builder.Property(template => template.ExternalTemplateId).HasMaxLength(160).IsRequired();
        builder.Property(template => template.Name).HasMaxLength(120).IsRequired();
        builder.Property(template => template.LanguageCode).HasMaxLength(20).IsRequired();
        builder.Property(template => template.Category).HasMaxLength(80).IsRequired();
        builder.Property(template => template.Status).HasMaxLength(80).IsRequired();
        builder.Property(template => template.HeaderType).HasMaxLength(40);
        builder.Property(template => template.BodyText).HasMaxLength(4000).IsRequired();
        builder.Property(template => template.FooterText).HasMaxLength(1000);
        builder.Property(template => template.ComponentsJson).HasMaxLength(16000).IsRequired();

        builder.HasOne(template => template.Company)
            .WithMany(company => company.WhatsAppTemplates)
            .HasForeignKey(template => template.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(template => template.WhatsAppIntegration)
            .WithMany(integration => integration.Templates)
            .HasForeignKey(template => template.WhatsAppIntegrationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(template => new { template.WhatsAppIntegrationId, template.Name, template.LanguageCode }).IsUnique();
        builder.HasIndex(template => template.CompanyId);
        builder.HasIndex(template => template.IsActive);
        builder.HasIndex(template => new { template.CompanyId, template.Status, template.IsActive });
    }
}
