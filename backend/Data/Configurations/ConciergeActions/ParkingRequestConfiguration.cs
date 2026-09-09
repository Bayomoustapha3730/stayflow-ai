using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations.ConciergeActions;

public sealed class ParkingRequestConfiguration : IEntityTypeConfiguration<ParkingRequest>
{
    public void Configure(EntityTypeBuilder<ParkingRequest> builder)
    {
        builder.ToTable("ParkingRequests");

        builder.HasKey(item => item.Id);
        // Property is checked via the entity's own required navigation (not Conversation.Property,
        // which is nullable and would ambiguously LEFT JOIN a soft-deleted property to the same
        // NULL result as "no property set", silently failing to hide the row).
        builder.HasQueryFilter(item =>
            !item.Conversation.IsDeleted
            && !item.Conversation.Guest.IsDeleted
            && !item.Property.IsDeleted);

        builder.Property(item => item.VehicleDescription).HasMaxLength(120);
        builder.Property(item => item.GuestNote).HasMaxLength(240);

        builder.HasIndex(item => new { item.CompanyId, item.PropertyId, item.Status });
        builder.HasIndex(item => new { item.ReservationId, item.CreatedAt });

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}
