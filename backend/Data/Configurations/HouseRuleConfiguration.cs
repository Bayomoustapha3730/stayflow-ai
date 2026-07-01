using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class HouseRuleConfiguration : IEntityTypeConfiguration<HouseRule>
{
    public void Configure(EntityTypeBuilder<HouseRule> builder)
    {
        builder.ToTable("HouseRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Title).HasMaxLength(160).IsRequired();
        builder.Property(rule => rule.Description).HasMaxLength(1000).IsRequired();

        builder.HasOne(rule => rule.Property)
            .WithMany(property => property.HouseRules)
            .HasForeignKey(rule => rule.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rule => rule.PropertyId);
        builder.HasIndex(rule => rule.CreatedAt);
    }
}
