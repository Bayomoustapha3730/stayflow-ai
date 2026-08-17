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
        builder.HasQueryFilter(payment => !payment.Property.IsDeleted && !payment.Guest.IsDeleted);

        // Amount and currency
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();

        // Provider information
        builder.Property(payment => payment.Provider).HasMaxLength(40).IsRequired();
        builder.Property(payment => payment.ProviderEnvironment).HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.PaymentMethod).HasMaxLength(40).IsRequired();

        // Provider identifiers
        builder.Property(payment => payment.ProviderRequestId).HasMaxLength(160);
        builder.Property(payment => payment.ProviderCheckoutRequestId).HasMaxLength(160);
        builder.Property(payment => payment.ProviderTransactionId).HasMaxLength(160);

        // Contact information
        builder.Property(payment => payment.CustomerPhoneNumber).HasMaxLength(32);

        // References
        builder.Property(payment => payment.ExternalReference).HasMaxLength(160);
        builder.Property(payment => payment.InternalReference).HasMaxLength(160);
        builder.Property(payment => payment.Status).HasMaxLength(40).IsRequired();

        // Failure details
        builder.Property(payment => payment.FailureCode).HasMaxLength(80);
        builder.Property(payment => payment.FailureMessage).HasMaxLength(500);

        // Foreign keys
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

        builder.HasOne(payment => payment.Reservation)
            .WithMany()
            .HasForeignKey(payment => payment.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(payment => payment.ServiceRequest)
            .WithMany(request => request.Payments)
            .HasForeignKey(payment => payment.ServiceRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for common queries
        builder.HasIndex(payment => payment.CompanyId);
        builder.HasIndex(payment => payment.PropertyId);
        builder.HasIndex(payment => payment.GuestId);
        builder.HasIndex(payment => payment.ReservationId);
        builder.HasIndex(payment => payment.CreatedAt);
        builder.HasIndex(payment => payment.Provider);

        // Indexes for provider callback correlation
        builder.HasIndex(payment => payment.ProviderCheckoutRequestId).IsUnique();
        builder.HasIndex(payment => payment.ProviderTransactionId).IsUnique();
        builder.HasIndex(payment => new { payment.CompanyId, payment.ExternalReference }).IsUnique();

        // Composite index for payment history queries
        builder.HasIndex(payment => new { payment.CompanyId, payment.ReservationId });
        builder.HasIndex(payment => new { payment.CompanyId, payment.GuestId });
    }
}
