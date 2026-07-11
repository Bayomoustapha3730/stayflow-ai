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
        builder.HasQueryFilter(message => !message.Conversation.Property.IsDeleted && !message.Conversation.Guest.IsDeleted);

        builder.Property(message => message.SenderType).HasMaxLength(40).IsRequired();
        builder.Property(message => message.Body).HasMaxLength(4000).IsRequired();
        builder.Property(message => message.AIOutcome).HasMaxLength(80);
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
        builder.HasIndex(message => message.CreatedAt);
        builder.HasIndex(message => new { message.CompanyId, message.ConversationId, message.CreatedAt });
    }
}
