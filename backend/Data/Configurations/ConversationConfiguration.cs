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
        builder.HasQueryFilter(conversation =>
            !conversation.IsDeleted
            && !conversation.Guest.IsDeleted
            && (conversation.Property == null || !conversation.Property.IsDeleted));

        builder.Property(conversation => conversation.Channel).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(conversation => conversation.ChannelIdentity).HasMaxLength(160);
        builder.Property(conversation => conversation.ExternalThreadId).HasMaxLength(160);
        builder.Property(conversation => conversation.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(conversation => conversation.Subject).HasMaxLength(200);
        builder.Property(conversation => conversation.EscalationReason).HasMaxLength(120);
        builder.Property(conversation => conversation.ReservationContextResolutionMethod).HasMaxLength(80);

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

        builder.HasOne(conversation => conversation.Reservation)
            .WithMany(reservation => reservation.Conversations)
            .HasForeignKey(conversation => conversation.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(conversation => conversation.AssignedUser)
            .WithMany(user => user.AssignedConversations)
            .HasForeignKey(conversation => conversation.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(conversation => conversation.CompanyId);
        builder.HasIndex(conversation => conversation.GuestId);
        builder.HasIndex(conversation => conversation.ReservationId);
        builder.HasIndex(conversation => conversation.PropertyId);
        builder.HasIndex(conversation => conversation.Status);
        builder.HasIndex(conversation => conversation.LastActivityAt);
        builder.HasIndex(conversation => conversation.CreatedAt);
        builder.HasIndex(conversation => conversation.IsDeleted);
        builder.HasIndex(conversation => new { conversation.CompanyId, conversation.Status, conversation.LastActivityAt });
        builder.HasIndex(conversation => new { conversation.CompanyId, conversation.GuestId, conversation.Status });
        builder.HasIndex(conversation => new { conversation.CompanyId, conversation.Channel, conversation.ChannelIdentity });
    }
}
