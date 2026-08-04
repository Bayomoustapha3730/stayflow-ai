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
        builder.Property(item => item.Reason).HasMaxLength(220).IsRequired();

        builder.HasIndex(item => new { item.CompanyId, item.Status, item.TriggeredAt });
        builder.HasIndex(item => new { item.CompanyId, item.ConversationId, item.Status });

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.SetNull);
    }
}
