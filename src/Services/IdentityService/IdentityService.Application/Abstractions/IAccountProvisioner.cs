using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Abstractions;

public interface IAccountProvisioner
{
    Task<AccountCreationResult> CreateTailorAccountAsync(
        EmailAddress email,
        string password,
        TailorProfileId tailorProfileId,
        CancellationToken cancellationToken = default);
}

public sealed record AccountCreationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static AccountCreationResult Success() => new(true, []);

    public static AccountCreationResult Failure(params string[] errors) => new(false, errors);
}
