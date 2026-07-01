using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data.Configurations;

public sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.ToTable("EmergencyContacts");
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Name).HasMaxLength(160).IsRequired();
        builder.Property(contact => contact.Role).HasMaxLength(100).IsRequired();
        builder.Property(contact => contact.PhoneNumber).HasMaxLength(32).IsRequired();

        builder.HasOne(contact => contact.Property)
            .WithMany(property => property.EmergencyContacts)
            .HasForeignKey(contact => contact.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(contact => contact.PropertyId);
        builder.HasIndex(contact => contact.PhoneNumber);
        builder.HasIndex(contact => contact.CreatedAt);
    }
}
