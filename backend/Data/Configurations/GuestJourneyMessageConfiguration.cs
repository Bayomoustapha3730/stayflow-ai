using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class GuestJourneyMessageConfiguration : IEntityTypeConfiguration<GuestJourneyMessage>
{
    public void Configure(EntityTypeBuilder<GuestJourneyMessage> builder)
    {
        builder.ToTable("GuestJourneyMessages");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.JourneyEventType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.Channel).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.Language).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ContentType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.RenderedContent).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.TemplateName).HasMaxLength(120);
        builder.Property(item => item.TemplateParametersJson).HasMaxLength(2000);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.ProviderMessageId).HasMaxLength(160);
        builder.Property(item => item.LastError).HasMaxLength(500);
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ReservationLifecycleEvent).WithMany().HasForeignKey(item => item.ReservationLifecycleEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Guest).WithMany().HasForeignKey(item => item.GuestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ConversationMessage).WithMany().HasForeignKey(item => item.ConversationMessageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.CompanyId, item.ReservationLifecycleEventId })
            .IsUnique()
            .HasDatabaseName("UX_GuestJourneyMessages_CompanyId_ReservationLifecycleEventId");
        builder.HasIndex(item => new { item.CompanyId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.CompanyId, item.Status, item.NextAttemptAtUtc });
        builder.HasIndex(item => new { item.CompanyId, item.ReservationId });
    }
}