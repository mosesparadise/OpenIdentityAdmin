using System.Security.Cryptography.X509Certificates;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;

/// <summary>Separates active issuance material from the bounded retained native verification/decryption inventory.</summary>
internal sealed class ClientStateKeySet(X509Certificate2 active, X509Certificate2[] previous)
{
    internal X509Certificate2 Active { get; } = active;
    internal IReadOnlyList<X509Certificate2> All { get; } = Array.AsReadOnly<X509Certificate2>([active, .. previous]);
}
