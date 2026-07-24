using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class PropertyKnowledgeArticleConfiguration : IEntityTypeConfiguration<PropertyKnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<PropertyKnowledgeArticle> builder)
    {
        builder.ToTable("PropertyKnowledgeArticles");

        builder.HasKey(article => article.Id);
        builder.HasQueryFilter(article => !article.IsDeleted && !article.Property.IsDeleted);
        builder.Property(article => article.Category)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(article => article.Title).HasMaxLength(200).IsRequired();
        builder.Property(article => article.Summary).HasMaxLength(280);
        builder.Property(article => article.Content).HasMaxLength(6000).IsRequired();
        builder.Property(article => article.Tags).HasMaxLength(500).IsRequired();
        builder.Property(article => article.Priority).HasDefaultValue(0);

        builder.HasOne(article => article.Company)
            .WithMany(company => company.PropertyKnowledgeArticles)
            .HasForeignKey(article => article.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(article => article.Property)
            .WithMany(property => property.PropertyKnowledgeArticles)
            .HasForeignKey(article => article.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(article => article.ApprovedByUser)
            .WithMany(user => user.ApprovedKnowledgeArticles)
            .HasForeignKey(article => article.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(article => article.CreatedByUser)
            .WithMany(user => user.CreatedKnowledgeArticles)
            .HasForeignKey(article => article.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(article => article.UpdatedByUser)
            .WithMany(user => user.UpdatedKnowledgeArticles)
            .HasForeignKey(article => article.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(article => article.DeletedByUser)
            .WithMany(user => user.DeletedKnowledgeArticles)
            .HasForeignKey(article => article.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(article => article.CompanyId);
        builder.HasIndex(article => article.PropertyId);
        builder.HasIndex(article => article.Category);
        builder.HasIndex(article => article.IsApproved);
        builder.HasIndex(article => article.IsActive);
        builder.HasIndex(article => article.UpdatedAt);
        builder.HasIndex(article => new { article.CompanyId, article.PropertyId, article.IsApproved, article.IsActive });
    }
}
