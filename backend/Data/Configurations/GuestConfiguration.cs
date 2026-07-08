using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.ToTable("Guests");

        builder.HasKey(guest => guest.Id);
        builder.HasQueryFilter(guest => !guest.IsDeleted);

        builder.Property(guest => guest.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(guest => guest.LastName).HasMaxLength(100).IsRequired();
        builder.Property(guest => guest.Email).HasMaxLength(254);
        builder.Property(guest => guest.PhoneNumber).HasMaxLength(32);
        builder.Property(guest => guest.PreferredLanguage).HasMaxLength(16).IsRequired();
        builder.Property(guest => guest.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(guest => guest.Notes).HasMaxLength(2000);

        builder.HasOne(guest => guest.Company)
            .WithMany(company => company.Guests)
            .HasForeignKey(guest => guest.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(guest => guest.CompanyId);
        builder.HasIndex(guest => guest.Email);
        builder.HasIndex(guest => guest.PhoneNumber);
        builder.HasIndex(guest => guest.CreatedAt);
        builder.HasIndex(guest => guest.IsDeleted);
        builder.HasIndex(guest => new { guest.CompanyId, guest.Email });
        builder.HasIndex(guest => new { guest.CompanyId, guest.PhoneNumber });
    }
}
