using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Contains the six public JWK members accepted for a locally registered RS256 signing key.</summary>
/// <example><code>var key = new ApplicationPublicKey("RSA", "service-2026", "RS256", "sig", modulus, exponent);</code></example>
public sealed record ApplicationPublicKey([property: Required] string Kty, [property: Required] string Kid, [property: Required] string Alg,
    [property: Required] string Use, [property: Required] string N, [property: Required] string E);

/// <summary>Registers a confidential machine client with the operator's public authentication key.</summary>
/// <example><code>var input = new ApplicationKeyMachineCreate(service, publicKey);</code></example>
public sealed record ApplicationKeyMachineCreate([property: Required] ApplicationMachineCreate Application, [property: Required] ApplicationPublicKey PublicKey);

/// <summary>Confirms replacement of one public key under the target application's current strong version.</summary>
/// <example><code>var rotation = new ApplicationKeyReplacement(publicKey, "2030-01-01T00:00:00Z", true);</code></example>
public sealed record ApplicationKeyReplacement([property: Required] ApplicationPublicKey PublicKey, [property: Required] string CredentialExpiresAt, bool Confirm);
