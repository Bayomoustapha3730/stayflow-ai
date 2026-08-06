using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("UsageRecords");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Metric).HasMaxLength(80).IsRequired();

        builder.HasOne(record => record.Company)
            .WithMany(company => company.UsageRecords)
            .HasForeignKey(record => record.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(record => new { record.CompanyId, record.Metric, record.PeriodStartUtc })
            .IsUnique();
        builder.HasIndex(record => new { record.CompanyId, record.Metric, record.PeriodEndUtc });
    }
}