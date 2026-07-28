using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations.ConciergeActions;

public sealed class ConciergeActionAuditLogConfiguration : IEntityTypeConfiguration<ConciergeActionAuditLog>
{
    public void Configure(EntityTypeBuilder<ConciergeActionAuditLog> builder)
    {
        builder.ToTable("ConciergeActionAuditLogs");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.ActorType).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Channel).HasMaxLength(30).IsRequired();
        builder.Property(item => item.ResultCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.CorrelationId).HasMaxLength(80).IsRequired();
        builder.Property(item => item.MetadataJson).HasMaxLength(1500);

        builder.HasIndex(item => new { item.CompanyId, item.ConversationId, item.CreatedAt });
        builder.HasIndex(item => new { item.PendingActionId, item.CreatedAt });

        builder.HasOne(item => item.Company).WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Conversation).WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PendingAction).WithMany().HasForeignKey(item => item.PendingActionId).OnDelete(DeleteBehavior.SetNull);
    }
}
