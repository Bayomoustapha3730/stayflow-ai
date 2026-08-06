using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");

        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Status).HasMaxLength(40).IsRequired();
        builder.Property(subscription => subscription.ExternalSubscriptionId).HasMaxLength(120);
        builder.Property(subscription => subscription.ExternalPriceId).HasMaxLength(120);
        builder.Property(subscription => subscription.Notes).HasMaxLength(500);

        builder.HasOne(subscription => subscription.Company)
            .WithMany(company => company.TenantSubscriptions)
            .HasForeignKey(subscription => subscription.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(subscription => subscription.SubscriptionPlan)
            .WithMany(plan => plan.TenantSubscriptions)
            .HasForeignKey(subscription => subscription.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(subscription => subscription.CompanyId);
        builder.HasIndex(subscription => subscription.SubscriptionPlanId);
        builder.HasIndex(subscription => new { subscription.CompanyId, subscription.Status });
        builder.HasIndex(subscription => subscription.ExternalSubscriptionId);
        builder.HasIndex(subscription => subscription.CompanyId)
            .HasFilter("\"Status\" IN ('Active','Trialing')")
            .IsUnique();
    }
}