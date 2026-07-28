using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations.ConciergeActions;

public sealed class PendingConciergeActionConfiguration : IEntityTypeConfiguration<PendingConciergeAction>
{
    public void Configure(EntityTypeBuilder<PendingConciergeAction> builder)
    {
        builder.ToTable("PendingConciergeActions");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.SerializedNormalizedParameters).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.FailureReasonCode).HasMaxLength(80);

        builder.HasIndex(item => new { item.CompanyId, item.ConversationId, item.Status });
        builder.HasIndex(item => item.ExpiresAt);
        builder.HasIndex(item => item.IdempotencyKey).IsUnique();

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
    }
}
