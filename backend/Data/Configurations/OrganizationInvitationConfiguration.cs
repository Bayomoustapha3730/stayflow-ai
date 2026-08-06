using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("OrganizationInvitations");

        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Email).HasMaxLength(254).IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(invitation => invitation.Role).HasMaxLength(40).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(128).IsRequired();

        builder.HasOne(invitation => invitation.Company)
            .WithMany(company => company.OrganizationInvitations)
            .HasForeignKey(invitation => invitation.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invitation => invitation.AcceptedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(invitation => invitation.CompanyId);
        builder.HasIndex(invitation => invitation.NormalizedEmail);
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => invitation.ExpiresAtUtc);
        builder.HasIndex(invitation => new { invitation.CompanyId, invitation.NormalizedEmail })
            .HasFilter("\"AcceptedAtUtc\" IS NULL AND \"RejectedAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");
    }
}