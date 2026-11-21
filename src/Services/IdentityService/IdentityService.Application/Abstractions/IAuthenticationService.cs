using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Abstractions;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(
        EmailAddress email,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed record AuthenticationResult(
    bool Succeeded,
    string? Token = null,
    DateTimeOffset? ExpiresAt = null,
    string? Error = null)
{
    public static AuthenticationResult Success(string token, DateTimeOffset expiresAt) =>
        new(true, token, expiresAt);

    public static AuthenticationResult Failed(string error) =>
        new(false, Error: error);

    public override string ToString() =>
        $"{nameof(AuthenticationResult)} {{ Succeeded = {Succeeded}, Token = [redacted], ExpiresAt = {ExpiresAt}, Error = {Error} }}";
}
