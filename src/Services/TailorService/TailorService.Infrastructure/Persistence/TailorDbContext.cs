using Microsoft.EntityFrameworkCore;
using TailorService.Domain.Entities;
using TailorService.Infrastructure.Persistence.Configurations;

namespace TailorService.Infrastructure.Persistence;

public sealed class TailorDbContext(DbContextOptions<TailorDbContext> options) : DbContext(options)
{
    public DbSet<Tailor> Tailors => Set<Tailor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new TailorConfiguration());

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

        foreach (var entry in ChangeTracker.Entries<Tailor>())
        {
            if (entry.State is EntityState.Added or EntityState.Deleted)
                continue;

            if (entry.State != EntityState.Modified && entry.Entity.DomainEvents.Count == 0)
                continue;

            entry.Property<int>("Version").CurrentValue += 1;
        }
    }
}
