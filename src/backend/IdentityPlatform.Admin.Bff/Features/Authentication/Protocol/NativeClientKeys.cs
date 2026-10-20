using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Selects explicitly active state material independently of native retained-certificate ordering.</summary>
internal sealed class NativeClientKeys(NativeClientCredentials credentials) : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Observes active selection inside the exact owned state-generation proof; assertions retain their registration-specific role.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof
            ? proof.RefreshCredentialsAsync(context, () => SelectAsync(context))
            : NativeClientAssertion.Find(context.Transaction) is { } assertion
                ? assertion.RefreshAsync(context, () => SelectAsync(context)) : SelectAsync(context);

    /// <summary>Rejects expired issuance material and lifetimes exceeding either policy or certificate validity.</summary>
    private ValueTask SelectAsync(OpenIddictClientEvents.GenerateTokenContext context)
    {
        var state = context.TokenType == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken;
        var assertion = context.TokenType == OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion;
        if (!state && !assertion)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var signingCertificate = state ? credentials.StateSigning.Active : credentials.Assertion;
        var inventory = state ? context.Options.SigningCredentials : context.Registration.SigningCredentials;
        var signing = inventory.SingleOrDefault(value => value.Key is X509SecurityKey key &&
            ReferenceEquals(key.Certificate, signingCertificate) && value.Algorithm == SecurityAlgorithms.RsaSha256);
        var encryption = context.Options.EncryptionCredentials.SingleOrDefault(value => value.Key is X509SecurityKey key &&
            ReferenceEquals(key.Certificate, credentials.StateEncryption.Active));
        var now = context.Options.TimeProvider!.GetUtcNow();
        var created = context.Principal.GetCreationDate();
        var expires = context.Principal.GetExpirationDate();
        var maximum = state ? AdministratorRegistrationPolicy.TokenLifetimeSeconds : AdministratorRegistrationPolicy.ProofLifetimeSeconds;
        if (signing is null || created is null || expires is null || created > now || expires <= now || expires <= created ||
            expires - created > TimeSpan.FromSeconds(maximum) || !Valid(signingCertificate, now, expires.Value) ||
            state && (encryption is null || !Valid(credentials.StateEncryption.Active, now, expires.Value)) ||
            !state && context.EncryptionCredentials is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.SigningCredentials = signing;
        context.EncryptionCredentials = state ? encryption : null;
        return ValueTask.CompletedTask;
    }

    /// <summary>Requires active credentials to cover the complete issued artifact lifetime.</summary>
    private static bool Valid(X509Certificate2 certificate, DateTimeOffset now, DateTimeOffset expires) =>
        now >= certificate.NotBefore.ToUniversalTime() && now < certificate.NotAfter.ToUniversalTime() && expires <= certificate.NotAfter.ToUniversalTime();
}
