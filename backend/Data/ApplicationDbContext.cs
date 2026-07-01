using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<KnowledgeBaseItem> KnowledgeBaseItems => Set<KnowledgeBaseItem>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Models.ServiceProvider> ServiceProviders => Set<Models.ServiceProvider>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Payment> Payments => Set<Payment>();

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }

    private void UpdateAuditFields()
    {
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }
}
