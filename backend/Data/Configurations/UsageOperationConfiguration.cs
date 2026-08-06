using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class UsageOperationConfiguration : IEntityTypeConfiguration<UsageOperation>
{
    public void Configure(EntityTypeBuilder<UsageOperation> builder)
    {
        builder.ToTable("UsageOperations");

        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Metric).HasMaxLength(80).IsRequired();
        builder.Property(operation => operation.IdempotencyKey).HasMaxLength(160).IsRequired();

        builder.HasOne(operation => operation.Company)
            .WithMany(company => company.UsageOperations)
            .HasForeignKey(operation => operation.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(operation => new { operation.CompanyId, operation.Metric, operation.PeriodStartUtc, operation.IdempotencyKey })
            .IsUnique();
    }
}