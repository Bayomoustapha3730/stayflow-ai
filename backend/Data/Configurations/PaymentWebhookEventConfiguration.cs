using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Provider).HasMaxLength(40).IsRequired();
        builder.Property(item => item.EventId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.EventType).HasMaxLength(120).IsRequired();
        builder.Property(item => item.CheckoutRequestId).HasMaxLength(160);
        builder.Property(item => item.TransactionId).HasMaxLength(160);
        builder.Property(item => item.PayloadHash).HasMaxLength(128).IsRequired();

        // Unique constraint on (Provider, EventId) to detect exact duplicates
        builder.HasIndex(item => new { item.Provider, item.EventId }).IsUnique();
        
        // Indexes for callback correlation queries
        builder.HasIndex(item => item.CheckoutRequestId);
        builder.HasIndex(item => item.TransactionId);
        builder.HasIndex(item => item.EventCreatedAtUtc);
    }
}
