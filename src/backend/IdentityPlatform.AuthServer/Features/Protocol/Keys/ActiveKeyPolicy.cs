using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.Keys;

/// <summary>Selects the configured active issuer certificates while preserving native validation inventories and bounded token lifetimes.</summary>
/// <example><code>
/// // Invoke from the configured native GenerateTokenContext handler after credential attachment.
/// await ActiveKeyPolicy.ApplyAsync(context, configuredIssuerCredentials);
/// // A rejected context must remain rejected; no credential or token value is logged.
/// </code></example>
public static class ActiveKeyPolicy
{
    private const int CodeLifetimeSeconds = 60;
    private const int TokenLifetimeSeconds = 300;
    public const string SafeDescription = "The issuer cannot issue tokens with the configured key policy.";

    /// <summary>Observes the existing active-key selection only within an owned administrator generation.</summary>
    /// <example><code>await ActiveKeyPolicy.ApplyAsync(context, configuredIssuerCredentials);</code></example>
    public static ValueTask ApplyAsync(GenerateTokenContext context, IssuerCredentials credentials) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.RefreshCredentialsAsync(() => ApplyPolicyAsync(context, credentials)),
            () => ApplyPolicyAsync(context, credentials));

    /// <summary>Retains the configured certificate validity and token-lifetime policy.</summary>
    private static ValueTask ApplyPolicyAsync(GenerateTokenContext context, IssuerCredentials credentials)
    {
        var maximum = context.TokenType switch
        {
            TokenTypeIdentifiers.Private.AuthorizationCode => CodeLifetimeSeconds,
            TokenTypeIdentifiers.AccessToken or TokenTypeIdentifiers.IdentityToken or TokenTypeIdentifiers.Private.RequestToken => TokenLifetimeSeconds,
            _ => 0
        };
        var encrypted = context.TokenType is TokenTypeIdentifiers.Private.AuthorizationCode or TokenTypeIdentifiers.Private.RequestToken;
        var now = context.Options.TimeProvider!.GetUtcNow();
        var created = context.Principal.GetCreationDate();
        var expires = context.Principal.GetExpirationDate();
        var signing = context.Options.SigningCredentials.FirstOrDefault(value =>
            value.Key is X509SecurityKey key && key.Certificate == credentials.Signing && value.Algorithm == SecurityAlgorithms.RsaSha256);
        var encryption = context.Options.EncryptionCredentials.FirstOrDefault(value =>
            value.Key is X509SecurityKey key && key.Certificate == credentials.Encryption);

        if (maximum == 0 || created is null || expires is null || created > now || expires <= now ||
            expires <= created || expires - created > TimeSpan.FromSeconds(maximum) ||
            encrypted != (context.EncryptionCredentials is not null) || signing is null ||
            context.Options.SigningCredentials.Any(value => value.Algorithm != SecurityAlgorithms.RsaSha256) ||
            !ValidNow(credentials.Signing, now) || expires > credentials.Signing.NotAfter.ToUniversalTime() ||
            encrypted && (encryption is null || !ValidNow(credentials.Encryption, now)))
        {
            context.Reject(Errors.ServerError, SafeDescription);
            return ValueTask.CompletedTask;
        }

        context.SigningCredentials = signing;
        if (encrypted)
        {
            context.EncryptionCredentials = encryption;
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Requires the configured certificate to be valid at the observed issuance instant.</summary>
    private static bool ValidNow(X509Certificate2 certificate, DateTimeOffset now) =>
        now >= certificate.NotBefore.ToUniversalTime() && now < certificate.NotAfter.ToUniversalTime();
}
