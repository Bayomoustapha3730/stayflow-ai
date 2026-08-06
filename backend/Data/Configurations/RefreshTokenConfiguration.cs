using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.RevokedReason).HasMaxLength(160);
        builder.Property(token => token.CreatedByIpAddress).HasMaxLength(64);
        builder.Property(token => token.CreatedByUserAgent).HasMaxLength(256);

        builder.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(token => token.UserId);
        builder.HasIndex(token => token.SessionId);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.CreatedAt);
    }
}
