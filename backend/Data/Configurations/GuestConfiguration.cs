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

        builder.Property(guest => guest.FullName).HasMaxLength(160).IsRequired();
        builder.Property(guest => guest.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(guest => guest.Email).HasMaxLength(254);

        builder.HasOne(guest => guest.Company)
            .WithMany(company => company.Guests)
            .HasForeignKey(guest => guest.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(guest => guest.CompanyId);
        builder.HasIndex(guest => guest.PhoneNumber);
        builder.HasIndex(guest => guest.CreatedAt);
        builder.HasIndex(guest => new { guest.CompanyId, guest.PhoneNumber }).IsUnique();
    }
}
