using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ConversationMessageKnowledgeSourceConfiguration : IEntityTypeConfiguration<ConversationMessageKnowledgeSource>
{
    public void Configure(EntityTypeBuilder<ConversationMessageKnowledgeSource> builder)
    {
        builder.ToTable("ConversationMessageKnowledgeSources");

        builder.HasKey(source => source.Id);
        builder.Property(source => source.RelevanceReason).HasMaxLength(240);

        builder.HasOne(source => source.Company)
            .WithMany(company => company.ConversationMessageKnowledgeSources)
            .HasForeignKey(source => source.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(source => source.Conversation)
            .WithMany(conversation => conversation.MessageKnowledgeSources)
            .HasForeignKey(source => source.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(source => source.ConversationMessage)
            .WithMany(message => message.KnowledgeSources)
            .HasForeignKey(source => source.ConversationMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(source => source.PropertyKnowledgeArticle)
            .WithMany(article => article.ConversationMessageSources)
            .HasForeignKey(source => source.PropertyKnowledgeArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(source => source.CompanyId);
        builder.HasIndex(source => source.ConversationId);
        builder.HasIndex(source => source.ConversationMessageId);
        builder.HasIndex(source => source.PropertyKnowledgeArticleId);
        builder.HasIndex(source => new { source.ConversationMessageId, source.Rank });
        builder.HasIndex(source => new { source.CompanyId, source.ConversationId, source.ConversationMessageId });
        builder.HasIndex(source => new { source.ConversationMessageId, source.PropertyKnowledgeArticleId })
            .IsUnique();
    }
}