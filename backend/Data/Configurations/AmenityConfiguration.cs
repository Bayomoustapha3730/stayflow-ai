using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.ToTable("Amenities");
        builder.HasKey(amenity => amenity.Id);
        builder.Property(amenity => amenity.Name).HasMaxLength(120).IsRequired();
        builder.Property(amenity => amenity.Description).HasMaxLength(500);

        builder.HasOne(amenity => amenity.Property)
            .WithMany(property => property.Amenities)
            .HasForeignKey(amenity => amenity.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(amenity => amenity.PropertyId);
        builder.HasIndex(amenity => amenity.CreatedAt);
    }
}
