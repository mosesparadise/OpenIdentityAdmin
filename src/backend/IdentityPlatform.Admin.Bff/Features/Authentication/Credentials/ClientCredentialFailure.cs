namespace IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;

/// <summary>Exposes only a fixed startup category without paths, passwords, private keys or underlying cryptographic errors.</summary>
internal sealed class ClientCredentialFailure() : InvalidOperationException(SafeMessage)
{
    internal const string SafeMessage = "Administrator client credentials are unavailable.";
}
