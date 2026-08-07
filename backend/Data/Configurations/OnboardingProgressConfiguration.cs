using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class OnboardingProgressConfiguration : IEntityTypeConfiguration<OnboardingProgress>
{
    public void Configure(EntityTypeBuilder<OnboardingProgress> builder)
    {
        builder.ToTable("OnboardingProgress");

        builder.HasKey(progress => progress.Id);
        builder.Property(progress => progress.CurrentStep).HasMaxLength(64).IsRequired();
        builder.Property(progress => progress.CompletedStepsCsv).HasMaxLength(1000).IsRequired();
        builder.Property(progress => progress.SkippedStepsCsv).HasMaxLength(1000).IsRequired();
        builder.Property(progress => progress.SelectedPlanName).HasMaxLength(120);
        builder.Property(progress => progress.Version).IsRequired();

        builder.HasOne(progress => progress.Company)
            .WithMany(company => company.OnboardingProgressRecords)
            .HasForeignKey(progress => progress.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(progress => progress.CompanyId);
        builder.HasIndex(progress => progress.UserId);
        builder.HasIndex(progress => new { progress.CompanyId, progress.UserId }).IsUnique();
        builder.HasIndex(progress => progress.CurrentStep);
        builder.HasIndex(progress => progress.IsCompleted);
        builder.HasIndex(progress => progress.CompletedByUserId);
    }
}