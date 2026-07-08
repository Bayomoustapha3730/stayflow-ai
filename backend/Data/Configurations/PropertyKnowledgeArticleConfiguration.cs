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
        builder.HasQueryFilter(article => !article.Property.IsDeleted);
        builder.Property(article => article.Title).HasMaxLength(200).IsRequired();
        builder.Property(article => article.Content).IsRequired();

        builder.HasOne(article => article.Company)
            .WithMany(company => company.PropertyKnowledgeArticles)
            .HasForeignKey(article => article.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(article => article.Property)
            .WithMany(property => property.PropertyKnowledgeArticles)
            .HasForeignKey(article => article.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(article => article.CompanyId);
        builder.HasIndex(article => article.PropertyId);
        builder.HasIndex(article => article.CreatedAt);
    }
}
