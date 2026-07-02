using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class KnowledgeBaseItemConfiguration : IEntityTypeConfiguration<KnowledgeBaseItem>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseItem> builder)
    {
        builder.ToTable("KnowledgeBaseItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Content).IsRequired();

        builder.HasOne(item => item.Company)
            .WithMany(company => company.KnowledgeBaseItems)
            .HasForeignKey(item => item.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Property)
            .WithMany()
            .HasForeignKey(item => item.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.CompanyId);
        builder.HasIndex(item => item.PropertyId);
        builder.HasIndex(item => item.CreatedAt);
    }
}
