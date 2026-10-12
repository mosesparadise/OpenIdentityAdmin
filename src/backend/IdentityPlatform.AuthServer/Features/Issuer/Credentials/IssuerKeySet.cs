using System.Security.Cryptography.X509Certificates;

namespace IdentityPlatform.AuthServer.Features.Issuer.Credentials;

/// <summary>Explicit issuance selection and retained native verification/decryption inventory.</summary>
public sealed class IssuerKeySet
{
    internal IssuerKeySet(X509Certificate2 active, X509Certificate2? staged, X509Certificate2[] previous)
    {
        Active = active;
        Staged = staged;
        Previous = Array.AsReadOnly(previous);
        All = Array.AsReadOnly<X509Certificate2>([active, .. staged is null ? Array.Empty<X509Certificate2>() : [staged], .. previous]);
    }

    public X509Certificate2 Active
    {
        get;
    }
    public X509Certificate2? Staged
    {
        get;
    }
    public IReadOnlyList<X509Certificate2> Previous
    {
        get;
    }
    public IReadOnlyList<X509Certificate2> All
    {
        get;
    }
}
