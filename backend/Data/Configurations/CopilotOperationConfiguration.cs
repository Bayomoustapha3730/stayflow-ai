using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class CopilotOperationConfiguration : IEntityTypeConfiguration<CopilotOperation>
{
    public void Configure(EntityTypeBuilder<CopilotOperation> builder)
    {
        builder.ToTable("CopilotOperations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.OperationType).HasConversion<string>().HasMaxLength(60).IsRequired();
        builder.Property(operation => operation.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(operation => operation.Company)
            .WithMany(company => company.CopilotOperations)
            .HasForeignKey(operation => operation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.Conversation)
            .WithMany(conversation => conversation.CopilotOperations)
            .HasForeignKey(operation => operation.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.ActorUser)
            .WithMany(user => user.CopilotOperations)
            .HasForeignKey(operation => operation.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(operation => operation.CompanyId);
        builder.HasIndex(operation => operation.ConversationId);
        builder.HasIndex(operation => operation.ActorUserId);
        builder.HasIndex(operation => new { operation.CompanyId, operation.ConversationId, operation.ActorUserId, operation.OperationType });
    }
}
