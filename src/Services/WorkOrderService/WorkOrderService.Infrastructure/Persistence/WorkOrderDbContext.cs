using Microsoft.EntityFrameworkCore;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Infrastructure.Persistence.Configurations;
using WorkOrderService.Infrastructure.Persistence.Entities;

namespace WorkOrderService.Infrastructure.Persistence;

public class WorkOrderDbContext(DbContextOptions<WorkOrderDbContext> options) : DbContext(options)
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<AssignableTailorProjection> AssignableTailors => Set<AssignableTailorProjection>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("work_order_serial_number")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.ApplyConfiguration(new WorkOrderConfiguration());
        modelBuilder.ApplyConfiguration(new AssignableTailorProjectionConfiguration());
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

        foreach (var entry in ChangeTracker.Entries<WorkOrder>())
        {
            if (entry.State is EntityState.Added or EntityState.Deleted)
                continue;
                
            var changed = entry.State == EntityState.Modified || entry.Entity.DomainEvents.Count > 0;
            if (!changed)
                continue;

            entry.Property<int>("Version").CurrentValue += 1;
        }
    }
}
