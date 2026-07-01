using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Name).HasMaxLength(160).IsRequired();
        builder.Property(company => company.LegalName).HasMaxLength(220);
        builder.Property(company => company.Email).HasMaxLength(254);
        builder.Property(company => company.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(company => company.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(company => company.TimeZone).HasMaxLength(80).IsRequired();

        builder.HasIndex(company => company.PhoneNumber);
        builder.HasIndex(company => company.CreatedAt);
    }
}
