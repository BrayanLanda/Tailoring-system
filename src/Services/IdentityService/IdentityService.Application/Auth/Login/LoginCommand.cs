namespace IdentityService.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password)
{
    public override string ToString() =>
        $"{nameof(LoginCommand)} {{ Email = {Email}, Password = [redacted] }}";
}
