using TailorService.Domain.Common;
using TailorService.Domain.Events;
using TailorService.Domain.Exceptions;
using TailorService.Domain.ValueObjects;

namespace TailorService.Domain.Entities;

public sealed class Tailor : AggregateRoot
{
    private Tailor() { }

    public TailorId Id { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public static Tailor Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tailor name is required.");

        var tailor = new Tailor
        {
            Id = TailorId.New(),
            Name = name.Trim(),
            IsActive = true
        };

        tailor.Raise(new TailorRegistered(tailor.Id, tailor.Name));
        return tailor;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        Raise(new TailorDeactivated(Id));
    }

    public void Reactivate()
    {
        if (IsActive)
            return;

        IsActive = true;
        Raise(new TailorReactivated(Id));
    }
}
