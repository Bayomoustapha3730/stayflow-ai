using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class LocalRecommendationConfiguration : IEntityTypeConfiguration<LocalRecommendation>
{
    public void Configure(EntityTypeBuilder<LocalRecommendation> builder)
    {
        builder.ToTable("LocalRecommendations");
        builder.HasKey(recommendation => recommendation.Id);
        builder.Property(recommendation => recommendation.Name).HasMaxLength(160).IsRequired();
        builder.Property(recommendation => recommendation.Category).HasMaxLength(100).IsRequired();
        builder.Property(recommendation => recommendation.Description).HasMaxLength(1000);
        builder.Property(recommendation => recommendation.Address).HasMaxLength(240);
        builder.Property(recommendation => recommendation.PhoneNumber).HasMaxLength(32);

        builder.HasOne(recommendation => recommendation.Property)
            .WithMany(property => property.LocalRecommendations)
            .HasForeignKey(recommendation => recommendation.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(recommendation => recommendation.PropertyId);
        builder.HasIndex(recommendation => recommendation.CreatedAt);
    }
}
