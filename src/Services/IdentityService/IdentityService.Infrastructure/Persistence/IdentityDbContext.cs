using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence.Configurations;
using IdentityService.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<TailorInvitation> TailorInvitations => Set<TailorInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new TailorInvitationConfiguration());

        modelBuilder.Entity<AppUser>(b =>
        {
            b.Property(u => u.TailorProfileId).IsRequired(false);
            b.HasIndex(u => u.TailorProfileId).IsUnique().HasFilter("tailor_profile_id IS NOT NULL");
        });
    }

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
