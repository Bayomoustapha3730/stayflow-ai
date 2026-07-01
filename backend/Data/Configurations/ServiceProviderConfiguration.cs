using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;
using ServiceProviderEntity = StayFlow.Api.Models.ServiceProvider;

namespace StayFlow.Api.Data.Configurations;

public sealed class ServiceProviderConfiguration : IEntityTypeConfiguration<ServiceProviderEntity>
{
    public void Configure(EntityTypeBuilder<ServiceProviderEntity> builder)
    {
        builder.ToTable("ServiceProviders");

        builder.HasKey(provider => provider.Id);

        builder.Property(provider => provider.Name).HasMaxLength(160).IsRequired();
        builder.Property(provider => provider.Category).HasMaxLength(100).IsRequired();
        builder.Property(provider => provider.PhoneNumber).HasMaxLength(32).IsRequired();

        builder.HasOne(provider => provider.Company)
            .WithMany(company => company.ServiceProviders)
            .HasForeignKey(provider => provider.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(provider => provider.CompanyId);
        builder.HasIndex(provider => provider.PhoneNumber);
        builder.HasIndex(provider => provider.CreatedAt);
    }
}
