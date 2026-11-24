using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Security;

public sealed class JwtSigningMaterial : IDisposable
{
    private readonly RSA _rsa;

    public JwtSigningMaterial(string privateKeyPem)
    {
        _rsa = RSA.Create();
        try
        {
            _rsa.ImportFromPem(privateKeyPem);
            _ = _rsa.ExportParameters(includePrivateParameters: true);
            if (_rsa.KeySize < 2048)
                throw new InvalidOperationException("Jwt:PrivateKeyPem must contain an RSA key of at least 2048 bits.");
        }
        catch
        {
            _rsa.Dispose();
            throw;
        }

        var keyId = Convert.ToHexString(SHA256.HashData(_rsa.ExportSubjectPublicKeyInfo()))[..16];
        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = keyId };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
    }

    public RsaSecurityKey SecurityKey { get; }
    public SigningCredentials SigningCredentials { get; }

    public void Dispose() => _rsa.Dispose();
}
