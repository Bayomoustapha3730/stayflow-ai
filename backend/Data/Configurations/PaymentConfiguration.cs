using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
        builder.Property(payment => payment.ExternalReference).HasMaxLength(160);
        builder.Property(payment => payment.Status).HasMaxLength(40).IsRequired();

        builder.HasOne(payment => payment.Company)
            .WithMany(company => company.Payments)
            .HasForeignKey(payment => payment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.Property)
            .WithMany(property => property.Payments)
            .HasForeignKey(payment => payment.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.Guest)
            .WithMany(guest => guest.Payments)
            .HasForeignKey(payment => payment.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.ServiceRequest)
            .WithMany(request => request.Payments)
            .HasForeignKey(payment => payment.ServiceRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(payment => payment.CompanyId);
        builder.HasIndex(payment => payment.PropertyId);
        builder.HasIndex(payment => payment.GuestId);
        builder.HasIndex(payment => payment.CreatedAt);
    }
}
