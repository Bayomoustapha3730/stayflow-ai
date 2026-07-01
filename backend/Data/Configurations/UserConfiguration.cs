using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.FullName).HasMaxLength(160).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(user => user.Role).HasMaxLength(80).IsRequired();

        builder.HasOne(user => user.Company)
            .WithMany(company => company.Users)
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(user => user.CompanyId);
        builder.HasIndex(user => user.PhoneNumber);
        builder.HasIndex(user => user.CreatedAt);
        builder.HasIndex(user => new { user.CompanyId, user.Email }).IsUnique();
    }
}
