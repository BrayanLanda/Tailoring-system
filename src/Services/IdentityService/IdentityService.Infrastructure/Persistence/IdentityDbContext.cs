using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<TailorInvitation> TailorInvitations => Set<TailorInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new TailorInvitationConfiguration());

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        BumpVersionOfChangedAggregates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void BumpVersionOfChangedAggregates()
    {
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker.Entries<TailorInvitation>())
        {
            if (entry.State is EntityState.Added or EntityState.Deleted)
                continue;

            if (entry.State != EntityState.Modified && entry.Entity.DomainEvents.Count == 0)
                continue;

            entry.Property<int>("Version").CurrentValue += 1;
        }
    }
}
