using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(property => property.Id);

        builder.Property(property => property.Name).HasMaxLength(180).IsRequired();
        builder.Property(property => property.AddressLine1).HasMaxLength(240).IsRequired();
        builder.Property(property => property.City).HasMaxLength(120).IsRequired();
        builder.Property(property => property.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(property => property.TimeZone).HasMaxLength(80).IsRequired();

        builder.HasOne(property => property.Company)
            .WithMany(company => company.Properties)
            .HasForeignKey(property => property.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(property => property.CompanyId);
        builder.HasIndex(property => property.CreatedAt);
    }
}
