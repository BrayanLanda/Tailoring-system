using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

internal sealed class FakeAccountProvisioner : IAccountProvisioner
{
    public AccountCreationResult Result { get; set; } = AccountCreationResult.Success();
    public List<ProvisionedAccount> Accounts { get; } = [];

    public Task<AccountCreationResult> CreateTailorAccountAsync(
        EmailAddress email,
        string password,
        TailorProfileId tailorProfileId,
        CancellationToken cancellationToken = default)
    {
        if (Result.Succeeded)
            Accounts.Add(new ProvisionedAccount(email, password, tailorProfileId));

        return Task.FromResult(Result);
    }
}

internal sealed record ProvisionedAccount(EmailAddress Email, string Password, TailorProfileId TailorProfileId);
