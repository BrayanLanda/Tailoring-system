namespace IdentityService.Api.Auth;

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt)
{
    public override string ToString() =>
        $"{nameof(LoginResponse)} {{ Token = [redacted], ExpiresAt = {ExpiresAt} }}";
}
