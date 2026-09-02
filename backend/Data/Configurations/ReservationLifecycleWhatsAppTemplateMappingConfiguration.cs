using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ReservationLifecycleWhatsAppTemplateMappingConfiguration : IEntityTypeConfiguration<ReservationLifecycleWhatsAppTemplateMapping>
{
    public void Configure(EntityTypeBuilder<ReservationLifecycleWhatsAppTemplateMapping> builder)
    {
        builder.ToTable("ReservationLifecycleWhatsAppTemplateMappings");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.JourneyEventType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.LanguageCode).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ParameterBindings).HasMaxLength(400).IsRequired();

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WhatsAppIntegration).WithMany().HasForeignKey(item => item.WhatsAppIntegrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WhatsAppTemplate).WithMany().HasForeignKey(item => item.WhatsAppTemplateId).OnDelete(DeleteBehavior.Restrict);

        // One mapping per tenant integration + lifecycle event type + language keeps resolution
        // unambiguous while allowing a language-specific mapping alongside one LanguageCode=""
        // fallback per event type.
        builder.HasIndex(item => new { item.CompanyId, item.WhatsAppIntegrationId, item.JourneyEventType, item.LanguageCode })
            .IsUnique()
            .HasDatabaseName("UX_ReservationLifecycleWhatsAppTemplateMappings_Company_Integration_EventType_Language");
    }
}
