using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("ServiceRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Title).HasMaxLength(180).IsRequired();
        builder.Property(request => request.Description).HasMaxLength(2000);
        builder.Property(request => request.Status).HasMaxLength(40).IsRequired();

        builder.HasOne(request => request.Company)
            .WithMany(company => company.ServiceRequests)
            .HasForeignKey(request => request.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.Property)
            .WithMany(property => property.ServiceRequests)
            .HasForeignKey(request => request.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.Guest)
            .WithMany(guest => guest.ServiceRequests)
            .HasForeignKey(request => request.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.Conversation)
            .WithMany(conversation => conversation.ServiceRequests)
            .HasForeignKey(request => request.ConversationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(request => request.ServiceProvider)
            .WithMany(provider => provider.ServiceRequests)
            .HasForeignKey(request => request.ServiceProviderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(request => request.CompanyId);
        builder.HasIndex(request => request.PropertyId);
        builder.HasIndex(request => request.GuestId);
        builder.HasIndex(request => request.CreatedAt);
    }
}
