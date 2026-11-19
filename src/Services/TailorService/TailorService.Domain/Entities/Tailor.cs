using TailorService.Domain.Common;
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

        return new Tailor
        {
            Id = TailorId.New(),
            Name = name.Trim(),
            IsActive = true
        };
    }
}
