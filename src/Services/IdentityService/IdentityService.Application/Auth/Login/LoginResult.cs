namespace IdentityService.Application.Auth.Login;

public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt)
{
    public override string ToString() =>
        $"{nameof(LoginResult)} {{ Token = [redacted], ExpiresAt = {ExpiresAt} }}";
}
