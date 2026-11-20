namespace IdentityService.Application.Exceptions;

public sealed class AccountCreationFailedException(IReadOnlyList<string> errors) : Exception("The account could not be create")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
