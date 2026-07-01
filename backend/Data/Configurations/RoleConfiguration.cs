using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(80).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(300);
        builder.HasIndex(role => role.Name).IsUnique();
        builder.HasIndex(role => role.CreatedAt);
    }
}
