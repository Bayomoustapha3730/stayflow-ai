using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PropertyRecommendationConfiguration : IEntityTypeConfiguration<PropertyRecommendation>
{
    public void Configure(EntityTypeBuilder<PropertyRecommendation> builder)
    {
        builder.ToTable("PropertyRecommendations");
        builder.HasKey(recommendation => recommendation.Id);
        builder.HasQueryFilter(recommendation => !recommendation.Property.IsDeleted);
        builder.Property(recommendation => recommendation.Name).HasMaxLength(160).IsRequired();
        builder.Property(recommendation => recommendation.Category).HasMaxLength(100).IsRequired();
        builder.Property(recommendation => recommendation.Description).HasMaxLength(1000);
        builder.Property(recommendation => recommendation.Address).HasMaxLength(240);
        builder.Property(recommendation => recommendation.PhoneNumber).HasMaxLength(32);

        builder.HasOne(recommendation => recommendation.Property)
            .WithMany(property => property.PropertyRecommendations)
            .HasForeignKey(recommendation => recommendation.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(recommendation => recommendation.PropertyId);
        builder.HasIndex(recommendation => recommendation.CreatedAt);
    }
}
