using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        builder.ToTable("PlanEntitlements");

        builder.HasKey(entitlement => entitlement.Id);
        builder.Property(entitlement => entitlement.Key).HasMaxLength(120).IsRequired();
        builder.Property(entitlement => entitlement.Unit).HasMaxLength(40);
        builder.Property(entitlement => entitlement.Notes).HasMaxLength(500);

        builder.HasOne(entitlement => entitlement.SubscriptionPlan)
            .WithMany(plan => plan.Entitlements)
            .HasForeignKey(entitlement => entitlement.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entitlement => new { entitlement.SubscriptionPlanId, entitlement.Key }).IsUnique();
        builder.HasIndex(entitlement => entitlement.Key);
    }
}