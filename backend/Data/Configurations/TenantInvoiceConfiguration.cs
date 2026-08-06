using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class TenantInvoiceConfiguration : IEntityTypeConfiguration<TenantInvoice>
{
    public void Configure(EntityTypeBuilder<TenantInvoice> builder)
    {
        builder.ToTable("TenantInvoices");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.ExternalInvoiceId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.ExternalCustomerId).HasMaxLength(120);
        builder.Property(item => item.ExternalSubscriptionId).HasMaxLength(120);
        builder.Property(item => item.Status).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(12).IsRequired();

        builder.HasOne(item => item.Company)
            .WithMany(company => company.TenantInvoices)
            .HasForeignKey(item => item.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => item.CompanyId);
        builder.HasIndex(item => item.ExternalInvoiceId).IsUnique();
        builder.HasIndex(item => new { item.CompanyId, item.Status });
        builder.HasIndex(item => item.FailedAtUtc);
    }
}