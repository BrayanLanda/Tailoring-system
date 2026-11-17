using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Domain.ValueObjects;

public sealed record AlterationJob
{
    public string Description { get; }
    public int Quantity { get; }

    private AlterationJob(string description, int quantity)
    {
        Description = description;
        Quantity = quantity;
    }

    public static AlterationJob Of(string description, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Job description is required.");

        if (quantity <= 0)
            throw new DomainException("Job quantity must be greater than zero.");

        return new AlterationJob(description.Trim(), quantity);
    }
}
