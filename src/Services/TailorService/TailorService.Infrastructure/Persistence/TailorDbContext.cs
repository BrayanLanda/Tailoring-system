using System.Text.Json;
using Contracts.Tailors;
using Microsoft.EntityFrameworkCore;
using TailorService.Domain.Common;
using TailorService.Domain.Entities;
using TailorService.Domain.Events;
using TailorService.Infrastructure.Persistence.Configurations;

namespace TailorService.Infrastructure.Persistence;

public sealed class TailorDbContext(DbContextOptions<TailorDbContext> options) : DbContext(options)
{
    public DbSet<Tailor> Tailors => Set<Tailor>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TailorConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var stagedMessages = StageDomainEvents();
        try
        {
            var changes = base.SaveChanges(acceptAllChangesOnSuccess);
            ClearDomainEvents();
            return changes;
        }
        catch
        {
            OutboxMessages.RemoveRange(stagedMessages);
            throw;
        }
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var stagedMessages = StageDomainEvents();
        return SaveAndFinalizeAsync(stagedMessages, acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task<int> SaveAndFinalizeAsync(
        List<OutboxMessage> stagedMessages,
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        try
        {
            var changes = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            ClearDomainEvents();
            return changes;
        }
        catch
        {
            OutboxMessages.RemoveRange(stagedMessages);
            throw;
        }
    }

    private List<OutboxMessage> StageDomainEvents()
    {
        ChangeTracker.DetectChanges();
        BumpVersionOfChangedAggregates();

        var messages = ChangeTracker.Entries<Tailor>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .Select(MapToOutboxMessage)
            .ToList();

        OutboxMessages.AddRange(messages);
        return messages;
    }

    private void ClearDomainEvents()
    {
        foreach (var entry in ChangeTracker.Entries<Tailor>())
            entry.Entity.ClearDomainEvents();
    }

    private static OutboxMessage MapToOutboxMessage(IDomainEvent domainEvent)
    {
        var eventId = Guid.CreateVersion7();
        var occurredOn = DateTimeOffset.UtcNow;
        return domainEvent switch
        {
            TailorRegistered registered => Create(
                "tailor.registered.v1",
                new TailorRegisteredIntegrationEvent(eventId, registered.TailorId.Value, registered.Name, occurredOn)),
            TailorDeactivated deactivated => Create(
                "tailor.deactivated.v1",
                new TailorDeactivatedIntegrationEvent(eventId, deactivated.TailorId.Value, occurredOn)),
            TailorReactivated reactivated => Create(
                "tailor.reactivated.v1",
                new TailorReactivatedIntegrationEvent(eventId, reactivated.TailorId.Value, occurredOn)),
            _ => throw new InvalidOperationException($"Unsupported domain event: {domainEvent.GetType().Name}")
        };

        OutboxMessage Create<TEvent>(string eventType, TEvent integrationEvent) =>
            new(eventId, eventType, JsonSerializer.Serialize(integrationEvent), occurredOn);
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
