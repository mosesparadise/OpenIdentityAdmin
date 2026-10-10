namespace IdentityPlatform.AuthServer.Features.Issuer.Credentials;

// Do not attach the underlying exception: crypto/filesystem messages can contain configuration values.
public sealed class IssuerProtectionException() : Exception(SafeMessage)
{
    public const string SafeMessage = "Invalid issuer protection configuration or key ring. Check the issuer protection runbook.";
}
