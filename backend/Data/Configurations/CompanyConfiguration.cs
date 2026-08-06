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
        builder.Property(company => company.Slug).HasMaxLength(160).IsRequired();
        builder.Property(company => company.NormalizedSlug).HasMaxLength(160).IsRequired();
        builder.Property(company => company.Status).HasMaxLength(40).IsRequired();
        builder.Property(company => company.LegalName).HasMaxLength(220);
        builder.Property(company => company.Email).HasMaxLength(254);
        builder.Property(company => company.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(company => company.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(company => company.TimeZone).HasMaxLength(80).IsRequired();
        builder.Property(company => company.BrandingLogoUrl).HasMaxLength(500);
        builder.Property(company => company.BrandingPrimaryColor).HasMaxLength(32);
        builder.Property(company => company.OnboardingState).HasMaxLength(80);
        builder.Property(company => company.StripeCustomerId).HasMaxLength(120);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(company => company.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(company => company.PhoneNumber);
        builder.HasIndex(company => company.Slug).IsUnique();
        builder.HasIndex(company => company.NormalizedSlug).IsUnique();
        builder.HasIndex(company => company.Status);
        builder.HasIndex(company => company.StripeCustomerId);
        builder.HasIndex(company => company.CreatedAt);
    }
}
