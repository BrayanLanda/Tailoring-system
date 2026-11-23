using IdentityService.Application.Auth.Login;
using IdentityService.Application.Exceptions;
using IdentityService.Application.Tests.Fakes;
using IdentityService.Domain.Exceptions;

namespace IdentityService.Application.Tests.Auth;

public sealed class LoginHandlerTests
{
    private readonly FakeAuthenticationService _authService = new();
    private readonly LoginHandler _handler;
    private readonly DateTimeOffset _expiresAt = DateTimeOffset.UtcNow.AddHours(1);

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(_authService);
    }

    [Fact]
    public async Task ValidCredentials_ReturnsTokenAndExpiration()
    {
        const string email = "admin@sastreria.com";
        const string password = "StrongPassword123!";
        const string expectedToken = "valid.jwt.token";

        _authService.RegisterUser(email, password, expectedToken, _expiresAt);

        var result = await _handler.HandleAsync(new LoginCommand(email, password));

        Assert.Equal(expectedToken, result.Token);
        Assert.Equal(_expiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task InvalidPassword_ThrowsAuthenticationFailedException()
    {
        const string email = "admin@sastreria.com";
        _authService.RegisterUser(email, "CorrectPassword123!", "token", _expiresAt);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(new LoginCommand(email, "WrongPassword!")));

        Assert.Equal(LoginHandler.InvalidCredentialsMessage, ex.Message);
    }

    [Fact]
    public async Task UnknownEmail_ThrowsAuthenticationFailedException()
    {
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(new LoginCommand("nonexistent@sastreria.com", "Password123!")));

        Assert.Equal(LoginHandler.InvalidCredentialsMessage, ex.Message);
    }

    [Fact]
    public async Task InvalidEmailFormat_ThrowsDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(new LoginCommand("not-an-email", "Password123!")));
    }
}
