using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations.ConciergeActions;

public sealed class LateCheckoutRequestConfiguration : IEntityTypeConfiguration<LateCheckoutRequest>
{
    public void Configure(EntityTypeBuilder<LateCheckoutRequest> builder)
    {
        builder.ToTable("LateCheckoutRequests");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.GuestNote).HasMaxLength(240);
        builder.Property(item => item.DecisionNote).HasMaxLength(240);

        builder.HasIndex(item => new { item.CompanyId, item.PropertyId, item.Status });
        builder.HasIndex(item => new { item.ReservationId, item.CreatedAt });

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}
