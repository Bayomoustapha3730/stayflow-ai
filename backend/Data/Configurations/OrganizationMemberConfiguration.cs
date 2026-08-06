using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Role).HasMaxLength(40).IsRequired();
        builder.Property(member => member.Status).HasMaxLength(40).IsRequired();
        builder.Property(member => member.JoinedAt).IsRequired();

        builder.HasOne(member => member.Company)
            .WithMany(company => company.OrganizationMembers)
            .HasForeignKey(member => member.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany(user => user.OrganizationMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.InvitedByUser)
            .WithMany(user => user.OrganizationInvitesSent)
            .HasForeignKey(member => member.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(member => member.CompanyId);
        builder.HasIndex(member => member.UserId);
        builder.HasIndex(member => member.Role);
        builder.HasIndex(member => member.Status);
        builder.HasIndex(member => new { member.CompanyId, member.UserId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");
    }
}