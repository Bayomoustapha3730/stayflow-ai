using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ReservationLifecycleEventConfiguration : IEntityTypeConfiguration<ReservationLifecycleEvent>
{
    public void Configure(EntityTypeBuilder<ReservationLifecycleEvent> builder)
    {
        builder.ToTable("ReservationLifecycleEvents");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.EventType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.RuleVersion).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(500);

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Property).WithMany().HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reservation).WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Guest).WithMany().HasForeignKey(item => item.GuestId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.CompanyId, item.ReservationId });
        builder.HasIndex(item => new { item.CompanyId, item.Status, item.ScheduledForUtc });
        builder.HasIndex(item => new { item.CompanyId, item.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_ReservationLifecycleEvents_CompanyId_IdempotencyKey");
    }
}
