using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("ConversationMessages");

        builder.HasKey(message => message.Id);
        builder.HasQueryFilter(message =>
            !message.IsDeleted
            && !message.Conversation.IsDeleted
            && !message.Conversation.Guest.IsDeleted
            && (message.Conversation.Property == null || !message.Conversation.Property.IsDeleted));

        builder.Property(message => message.SenderType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(message => message.Content).HasMaxLength(4000).IsRequired();
        builder.Property(message => message.MessageType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(message => message.ExternalMessageId).HasMaxLength(160);
        builder.Property(message => message.ProviderName).HasMaxLength(80);
        builder.Property(message => message.ProviderModel).HasMaxLength(120);
        builder.Property(message => message.ProviderRequestId).HasMaxLength(160);
        builder.Property(message => message.AIOutcome).HasMaxLength(80);
        builder.Property(message => message.FailureCategory).HasMaxLength(80);
        builder.Property(message => message.EscalationReason).HasMaxLength(120);

        builder.HasOne(message => message.Company)
            .WithMany(company => company.ConversationMessages)
            .HasForeignKey(message => message.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(message => message.CompanyId);
        builder.HasIndex(message => message.ConversationId);
        builder.HasIndex(message => message.SentAt);
        builder.HasIndex(message => message.CreatedAt);
        builder.HasIndex(message => message.ExternalMessageId);
        builder.HasIndex(message => message.IsDeleted);
        builder.HasIndex(message => new { message.ConversationId, message.SentAt });
        builder.HasIndex(message => new { message.CompanyId, message.ConversationId, message.SentAt });
        builder.HasIndex(message => new { message.CompanyId, message.ExternalMessageId })
            .IsUnique()
            .HasFilter("\"ExternalMessageId\" IS NOT NULL");
    }
}
