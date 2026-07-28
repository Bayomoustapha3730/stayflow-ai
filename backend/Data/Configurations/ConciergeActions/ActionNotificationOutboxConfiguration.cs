using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations.ConciergeActions;

public sealed class ActionNotificationOutboxConfiguration : IEntityTypeConfiguration<ActionNotificationOutbox>
{
    public void Configure(EntityTypeBuilder<ActionNotificationOutbox> builder)
    {
        builder.ToTable("ActionNotificationOutbox");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.NotificationType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.PayloadReference).HasMaxLength(200).IsRequired();
        builder.Property(item => item.LastFailureCode).HasMaxLength(80);

        builder.HasIndex(item => new { item.Status, item.NextAttemptAt });

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
