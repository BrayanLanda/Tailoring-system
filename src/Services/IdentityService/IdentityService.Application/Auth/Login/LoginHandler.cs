using IdentityService.Application.Abstractions;
using IdentityService.Application.Exceptions;
using IdentityService.Domain.ValueObjects;

namespace IdentityService.Application.Auth.Login;

public sealed class LoginHandler(IAuthenticationService authenticationService)
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var email = EmailAddress.Of(command.Email);
        var result = await authenticationService.AuthenticateAsync(email, command.Password, cancellationToken);

        if (!result.Succeeded || result.Token is null || result.ExpiresAt is null)
        {
            throw new AuthenticationFailedException(result.Error ?? InvalidCredentialsMessage);
        }

        return new LoginResult(result.Token, result.ExpiresAt.Value);
    }
}
