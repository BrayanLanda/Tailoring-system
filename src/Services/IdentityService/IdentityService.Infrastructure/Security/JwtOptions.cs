namespace IdentityService.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "sastreria-identity";
    public string Audience { get; set; } = "sastreria-api";
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 120;
}
