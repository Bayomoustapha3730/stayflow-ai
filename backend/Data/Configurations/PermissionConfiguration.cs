using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Name).HasMaxLength(120).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(300);
        builder.HasIndex(permission => permission.Name).IsUnique();
        builder.HasIndex(permission => permission.CreatedAt);
    }
}
