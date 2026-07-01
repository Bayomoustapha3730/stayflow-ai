using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.EntityName).HasMaxLength(120).IsRequired();
        builder.Property(auditLog => auditLog.Action).HasMaxLength(80).IsRequired();
        builder.Property(auditLog => auditLog.Details).HasMaxLength(2000);

        builder.HasIndex(auditLog => auditLog.EntityId);
        builder.HasIndex(auditLog => auditLog.CreatedAt);
        builder.HasIndex(auditLog => new { auditLog.EntityName, auditLog.EntityId });
    }
}
