using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class TenantApiKeyConfiguration : IEntityTypeConfiguration<TenantApiKey>
{
    public void Configure(EntityTypeBuilder<TenantApiKey> builder)
    {
        builder.ToTable("TenantApiKeys");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.KeyPrefix).HasMaxLength(32).IsRequired();
        builder.Property(item => item.SecretHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.ScopesCsv).HasMaxLength(400).IsRequired();

        builder.HasOne(item => item.Company)
            .WithMany(company => company.TenantApiKeys)
            .HasForeignKey(item => item.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.CompanyId, item.Name });
        builder.HasIndex(item => item.KeyPrefix).IsUnique();
        builder.HasIndex(item => item.ExpiresAtUtc);
        builder.HasIndex(item => new { item.CompanyId, item.IsRevoked });
    }
}