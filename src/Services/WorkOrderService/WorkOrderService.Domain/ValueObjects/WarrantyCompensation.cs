using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Domain.ValueObjects;

public class WarrantyCompensation
{
    public Money Amount { get; }
    public string AuthorizedBy { get; }
    public DateTimeOffset AuthorizedAt { get; }

    private WarrantyCompensation(Money amount, string authorizedBy, DateTimeOffset authorizedAt)
    {
        Amount = amount;
        AuthorizedBy = authorizedBy;
        AuthorizedAt = authorizedAt;
    }

    public static WarrantyCompensation Of(Money amount, string authorizedBy, DateTimeOffset authorizedAt)
    {
        if (amount.Amount <= 0)
            throw new DomainException("Compensation must be greater than zero.");

        if (string.IsNullOrWhiteSpace(authorizedBy))
            throw new DomainException("The person who authorizes the compensation is required.");

        return new WarrantyCompensation(amount, authorizedBy.Trim(), authorizedAt);
    }
}
