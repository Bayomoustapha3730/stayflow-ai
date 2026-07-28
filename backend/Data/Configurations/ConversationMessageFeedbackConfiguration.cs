using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ConversationMessageFeedbackConfiguration : IEntityTypeConfiguration<ConversationMessageFeedback>
{
    public void Configure(EntityTypeBuilder<ConversationMessageFeedback> builder)
    {
        builder.ToTable("ConversationMessageFeedback");

        builder.HasKey(feedback => feedback.Id);
        builder.Property(feedback => feedback.FeedbackValue).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(feedback => feedback.Comment).HasMaxLength(500);

        builder.HasOne(feedback => feedback.Company)
            .WithMany(company => company.ConversationMessageFeedback)
            .HasForeignKey(feedback => feedback.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(feedback => feedback.Conversation)
            .WithMany(conversation => conversation.MessageFeedback)
            .HasForeignKey(feedback => feedback.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(feedback => feedback.ConversationMessage)
            .WithMany(message => message.Feedback)
            .HasForeignKey(feedback => feedback.ConversationMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(feedback => feedback.CompanyId);
        builder.HasIndex(feedback => feedback.ConversationId);
        builder.HasIndex(feedback => feedback.ConversationMessageId);
        builder.HasIndex(feedback => feedback.GuestId);
        builder.HasIndex(feedback => new { feedback.CompanyId, feedback.CreatedAt });
        builder.HasIndex(feedback => new { feedback.ConversationMessageId, feedback.GuestId }).IsUnique();
    }
}