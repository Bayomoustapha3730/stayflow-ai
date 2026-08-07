using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class OnboardingEventConfiguration : IEntityTypeConfiguration<OnboardingEvent>
{
    public void Configure(EntityTypeBuilder<OnboardingEvent> builder)
    {
        builder.ToTable("OnboardingEvents");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.EventName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Step).HasMaxLength(64);
        builder.Property(item => item.State).HasMaxLength(32);
        builder.Property(item => item.MetadataJson).HasMaxLength(2000).IsRequired();

        builder.HasOne(item => item.Company)
            .WithMany()
            .HasForeignKey(item => item.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => item.CompanyId);
        builder.HasIndex(item => item.UserId);
        builder.HasIndex(item => item.EventName);
        builder.HasIndex(item => new { item.CompanyId, item.CreatedAt });
    }
}
