using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ConversationParticipantReadStateConfiguration : IEntityTypeConfiguration<ConversationParticipantReadState>
{
    public void Configure(EntityTypeBuilder<ConversationParticipantReadState> builder)
    {
        builder.ToTable("ConversationParticipantReadStates");

        builder.HasKey(state => state.Id);
        builder.HasQueryFilter(state =>
            !state.Conversation.IsDeleted
            && !state.Conversation.Guest.IsDeleted
            && (state.Conversation.Property == null || !state.Conversation.Property.IsDeleted));

        builder.Property(state => state.ParticipantKind)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.HasOne(state => state.Company)
            .WithMany()
            .HasForeignKey(state => state.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(state => state.Conversation)
            .WithMany()
            .HasForeignKey(state => state.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(state => state.CompanyId);
        builder.HasIndex(state => state.ConversationId);
        builder.HasIndex(state => new { state.CompanyId, state.ParticipantKind, state.ParticipantId });
        builder.HasIndex(state => new { state.CompanyId, state.ConversationId, state.ParticipantKind, state.ParticipantId })
            .IsUnique();
    }
}
