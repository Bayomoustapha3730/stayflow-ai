using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PropertyHouseRuleConfiguration : IEntityTypeConfiguration<PropertyHouseRule>
{
    public void Configure(EntityTypeBuilder<PropertyHouseRule> builder)
    {
        builder.ToTable("PropertyHouseRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Title).HasMaxLength(160).IsRequired();
        builder.Property(rule => rule.Description).HasMaxLength(1000).IsRequired();

        builder.HasOne(rule => rule.Property)
            .WithMany(property => property.PropertyHouseRules)
            .HasForeignKey(rule => rule.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rule => rule.PropertyId);
        builder.HasIndex(rule => rule.CreatedAt);
    }
}
