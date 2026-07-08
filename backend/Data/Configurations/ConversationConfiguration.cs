using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(conversation => conversation.Id);
        builder.HasQueryFilter(conversation => !conversation.Property.IsDeleted && !conversation.Guest.IsDeleted);

        builder.Property(conversation => conversation.Channel).HasMaxLength(40).IsRequired();
        builder.Property(conversation => conversation.ExternalThreadId).HasMaxLength(160);
        builder.Property(conversation => conversation.Status).HasMaxLength(40).IsRequired();

        builder.HasOne(conversation => conversation.Company)
            .WithMany(company => company.Conversations)
            .HasForeignKey(conversation => conversation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(conversation => conversation.Property)
            .WithMany(property => property.Conversations)
            .HasForeignKey(conversation => conversation.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(conversation => conversation.Guest)
            .WithMany(guest => guest.Conversations)
            .HasForeignKey(conversation => conversation.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(conversation => conversation.CompanyId);
        builder.HasIndex(conversation => conversation.PropertyId);
        builder.HasIndex(conversation => conversation.GuestId);
        builder.HasIndex(conversation => conversation.CreatedAt);
    }
}
