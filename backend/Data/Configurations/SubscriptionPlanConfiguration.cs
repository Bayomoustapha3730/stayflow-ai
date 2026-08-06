using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");

        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Name).HasMaxLength(80).IsRequired();
        builder.Property(plan => plan.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(plan => plan.Description).HasMaxLength(500).IsRequired();

        builder.HasIndex(plan => plan.Name).IsUnique();
        builder.HasIndex(plan => plan.SortOrder);
    }
}