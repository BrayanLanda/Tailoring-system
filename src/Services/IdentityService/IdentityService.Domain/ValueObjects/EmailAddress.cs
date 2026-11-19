using System.Net.Mail;
using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.ValueObjects;

public readonly record struct EmailAddress
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("An email address is required.");

        var normalized = value.Trim().ToLowerInvariant();

        try
        {
            var parsed = new MailAddress(normalized);
            if (!string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("A valid email address is required.");
        }
        catch (FormatException)
        {
            throw new DomainException("A valid email address is required.");
        }

        return new EmailAddress(normalized);
    }
}
