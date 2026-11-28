using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Holds the original issuer-checked client identity, bounded claims and native signature key during one request.</summary>
internal sealed class SignedClientAssertion(string clientId, string identifier, long issued, long expires, long? notBefore, SecurityKey signingKey)
{
    internal string ClientId => clientId;
    internal string Identifier => identifier;
    internal long Issued => issued;
    internal long Expires => expires;
    internal long? NotBefore => notBefore;
    internal string? KeyId { get; } = signingKey.KeyId;
    internal string KeyThumbprint { get; } = Convert.ToHexStringLower(signingKey.ComputeJwkThumbprint());
}
