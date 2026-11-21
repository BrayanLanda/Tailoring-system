using IdentityService.Application.Abstractions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Tests.Fakes;

public sealed class FakeAuthenticationService : IAuthenticationService
{
    private readonly Dictionary<string, (string Password, string Token, DateTimeOffset ExpiresAt)> _users = new();

    public void RegisterUser(string email, string password, string token, DateTimeOffset expiresAt) =>
        _users[email.Trim().ToLowerInvariant()] = (password, token, expiresAt);

    public Task<AuthenticationResult> AuthenticateAsync(
        EmailAddress email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (_users.TryGetValue(email.Value, out var user) && user.Password == password)
        {
            return Task.FromResult(AuthenticationResult.Success(user.Token, user.ExpiresAt));
        }

        return Task.FromResult(AuthenticationResult.Failed("Invalid email or password."));
    }
}
