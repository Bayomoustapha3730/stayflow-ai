using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PropertyAmenityConfiguration : IEntityTypeConfiguration<PropertyAmenity>
{
    public void Configure(EntityTypeBuilder<PropertyAmenity> builder)
    {
        builder.ToTable("PropertyAmenities");
        builder.HasKey(PropertyAmenity => PropertyAmenity.Id);
        builder.HasQueryFilter(PropertyAmenity => !PropertyAmenity.Property.IsDeleted);
        builder.Property(PropertyAmenity => PropertyAmenity.Name).HasMaxLength(120).IsRequired();
        builder.Property(PropertyAmenity => PropertyAmenity.Description).HasMaxLength(500);

        builder.HasOne(PropertyAmenity => PropertyAmenity.Property)
            .WithMany(property => property.PropertyAmenities)
            .HasForeignKey(PropertyAmenity => PropertyAmenity.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(PropertyAmenity => PropertyAmenity.PropertyId);
        builder.HasIndex(PropertyAmenity => PropertyAmenity.CreatedAt);
    }
}
