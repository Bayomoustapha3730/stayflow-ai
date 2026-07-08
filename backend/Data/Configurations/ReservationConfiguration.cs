using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");

        builder.HasKey(reservation => reservation.Id);
        builder.HasQueryFilter(reservation => !reservation.IsDeleted);

        builder.Property(reservation => reservation.ExternalReservationReference).HasMaxLength(160);
        builder.Property(reservation => reservation.ReservationSource).HasMaxLength(80).IsRequired();
        builder.Property(reservation => reservation.ConfirmationNumber).HasMaxLength(80);
        builder.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(reservation => reservation.Currency).HasMaxLength(3);
        builder.Property(reservation => reservation.BookingAmount).HasPrecision(18, 2);
        builder.Property(reservation => reservation.SpecialRequests).HasMaxLength(2000);
        builder.Property(reservation => reservation.InternalNotes).HasMaxLength(2000);

        builder.HasOne(reservation => reservation.Company)
            .WithMany(company => company.Reservations)
            .HasForeignKey(reservation => reservation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reservation => reservation.Property)
            .WithMany(property => property.Reservations)
            .HasForeignKey(reservation => reservation.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reservation => reservation.PrimaryGuest)
            .WithMany(guest => guest.PrimaryReservations)
            .HasForeignKey(reservation => reservation.PrimaryGuestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reservation => reservation.CompanyId);
        builder.HasIndex(reservation => reservation.PropertyId);
        builder.HasIndex(reservation => reservation.PrimaryGuestId);
        builder.HasIndex(reservation => reservation.CreatedAt);
        builder.HasIndex(reservation => reservation.CheckInDate);
        builder.HasIndex(reservation => reservation.CheckOutDate);
        builder.HasIndex(reservation => reservation.ConfirmationNumber);
        builder.HasIndex(reservation => reservation.ExternalReservationReference);
        builder.HasIndex(reservation => reservation.IsDeleted);
        builder.HasIndex(reservation => new { reservation.CompanyId, reservation.ReservationSource, reservation.ExternalReservationReference });
        builder.HasIndex(reservation => new { reservation.CompanyId, reservation.ConfirmationNumber });
    }
}
