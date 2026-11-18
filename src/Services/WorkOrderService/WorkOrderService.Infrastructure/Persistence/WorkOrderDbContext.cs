using Microsoft.EntityFrameworkCore;
using WorkOrderService.Domain.Entities;
using WorkOrderService.Infrastructure.Persistence.Configurations;

namespace WorkOrderService.Infrastructure.Persistence;

public class WorkOrderDbContext(DbContextOptions<WorkOrderDbContext> options) : DbContext(options)
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WorkOrderConfiguration());
    }
}
