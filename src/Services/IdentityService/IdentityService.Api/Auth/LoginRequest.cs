namespace IdentityService.Api.Auth;

public sealed record LoginRequest(string Email, string Password)
{
    public override string ToString() =>
        $"{nameof(LoginRequest)} {{ Email = {Email}, Password = [redacted] }}";
}
