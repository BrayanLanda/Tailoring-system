using System.Text.RegularExpressions;
using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Domain.ValueObjects;

public sealed partial record PhoneNumber
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Of(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Phone number is required.");

        if (!AllowedCharacters().IsMatch(value.Trim()))
            throw new DomainException("Phone number contains invalid characters.");

        var digits = NonDigits().Replace(value, "");

        if (digits.Length == 12 && digits.StartsWith("57"))
            digits = digits[2..];

        if (!ColombianMobile().IsMatch(digits))
            throw new DomainException("Phone number must be a 10-digit mobile number starting with 3.");

        return new PhoneNumber(digits);
    }

    [GeneratedRegex(@"^\+?[0-9\s\-()]+$")]
    private static partial Regex AllowedCharacters();

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NonDigits();

    [GeneratedRegex("^3[0-9]{9}$")]
    private static partial Regex ColombianMobile();

    public override string ToString() => Value;
}
