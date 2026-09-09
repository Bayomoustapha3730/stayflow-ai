using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations.ConciergeActions;

public sealed class HostCopilotSlaAlertConfiguration : IEntityTypeConfiguration<HostCopilotSlaAlert>
{
    public void Configure(EntityTypeBuilder<HostCopilotSlaAlert> builder)
    {
        builder.ToTable("HostCopilotSlaAlerts");

        builder.HasKey(item => item.Id);
        // Property is checked via the entity's own required navigation (not Conversation.Property,
        // which is nullable and would ambiguously LEFT JOIN a soft-deleted property to the same
        // NULL result as "no property set", silently failing to hide the row).
        builder.HasQueryFilter(item =>
            !item.Conversation.IsDeleted
            && !item.Conversation.Guest.IsDeleted
            && !item.Property.IsDeleted);

        builder.Property(item => item.Reason).HasMaxLength(220).IsRequired();

        builder.HasIndex(item => new { item.CompanyId, item.Status, item.TriggeredAt });
        builder.HasIndex(item => new { item.CompanyId, item.ConversationId, item.Status });

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.SetNull);
    }
}
