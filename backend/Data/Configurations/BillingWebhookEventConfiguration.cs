using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class BillingWebhookEventConfiguration : IEntityTypeConfiguration<BillingWebhookEvent>
{
    public void Configure(EntityTypeBuilder<BillingWebhookEvent> builder)
    {
        builder.ToTable("BillingWebhookEvents");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Provider).HasMaxLength(40).IsRequired();
        builder.Property(item => item.EventId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.EventType).HasMaxLength(120).IsRequired();
        builder.Property(item => item.CustomerId).HasMaxLength(120);
        builder.Property(item => item.SubscriptionId).HasMaxLength(120);
        builder.Property(item => item.PayloadHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(item => new { item.Provider, item.EventId }).IsUnique();
        builder.HasIndex(item => item.EventCreatedAtUtc);
        builder.HasIndex(item => item.CustomerId);
        builder.HasIndex(item => item.SubscriptionId);
    }
}