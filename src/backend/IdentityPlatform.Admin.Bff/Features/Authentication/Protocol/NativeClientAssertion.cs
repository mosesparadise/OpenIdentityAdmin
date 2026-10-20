using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Observes one actual native assertion generation independently of the already released durable-state proof.</summary>
internal sealed class NativeClientAssertion
{
    private static readonly string Key = Guid.NewGuid().ToString();
    private readonly OpenIddictClientEvents.GenerateTokenContext context;
    private readonly ClaimsPrincipal principal;
    private readonly NativeClientProfile profile;
    private readonly ProtocolTokenPreparation preparation;
    private readonly string claims;
    private string? output;
    private bool completed;

    /// <summary>Requires the fixed native prepared assertion and a default crypto descriptor before observing any generation handler.</summary>
    private NativeClientAssertion(OpenIddictClientEvents.GenerateTokenContext context, NativeClientProfile profile)
    {
        this.context = context;
        this.profile = profile;
        principal = context.Principal;
        claims = ProtocolTokenDescriptorSnapshot.Claims(principal);
        preparation = new(context.SecurityTokenDescriptor, context.Options.JsonWebTokenHandler, typeof(JsonWebTokenHandler), encrypted: false,
            context.Registration.SigningCredentials, []);
        RequireCurrent(context);
        if (!string.IsNullOrEmpty(context.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Attaches once to the exact native transaction and requires real signing before dispatcher completion.</summary>
    internal static async ValueTask RunAsync(OpenIddictClientEvents.GenerateTokenContext context, NativeClientProfile profile, Func<ValueTask> native)
    {
        if (Find(context.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var proof = new NativeClientAssertion(context, profile);
        context.Transaction.SetProperty(Key, proof);
        await proof.ObserveAsync(context, native);
        proof.preparation.RequireReady(context.SecurityTokenDescriptor, context.SecurityTokenHandler);
        if (proof.output is null || proof.output != FactorBinding.Digest(context.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        proof.completed = true;
    }

    /// <summary>Retrieves only the private observer from the actual native transaction.</summary>
    internal static NativeClientAssertion? Find(OpenIddictClientTransaction transaction) => transaction.GetProperty<NativeClientAssertion>(Key);

    /// <summary>Observes only the installed native credential, subject and metadata handlers.</summary>
    internal ValueTask PrepareAsync(OpenIddictClientEvents.GenerateTokenContext current, ProtocolTokenPreparation.Stage stage, Func<ValueTask> native) =>
        ObserveAsync(current, () => preparation.AdvanceAsync(stage, current.SecurityTokenDescriptor, () => current.SecurityTokenHandler, native));

    /// <summary>Permits active credential selection only at the installed policy stage.</summary>
    internal ValueTask RefreshAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native) =>
        ObserveAsync(current, () => preparation.RefreshCredentialsAsync(current.SecurityTokenDescriptor, () => current.SecurityTokenHandler, native));

    /// <summary>Rejects preset output and records only the actual completed native cryptographic output digest.</summary>
    internal ValueTask SignAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native) => ObserveAsync(current, async () =>
    {
        preparation.RequireReady(current.SecurityTokenDescriptor, current.SecurityTokenHandler);
        if (output is not null || !string.IsNullOrEmpty(current.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        if (string.IsNullOrEmpty(current.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        output = FactorBinding.Digest(current.Token);
    });

    /// <summary>Binds the outgoing assertion bytes and original principal to the completed native generation and current strict lifetime.</summary>
    internal void RequireOutput(string? token, ClaimsPrincipal? prepared)
    {
        RequireCurrent(context);
        preparation.RequireReady(context.SecurityTokenDescriptor, context.SecurityTokenHandler);
        if (!completed || string.IsNullOrEmpty(token) || output != FactorBinding.Digest(token) || !ReferenceEquals(prepared, principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Latches all observed failures in the enclosing request; no database lock is held around assertion signing.</summary>
    private async ValueTask ObserveAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native)
    {
        try
        {
            RequireCurrent(current);
            await native();
            RequireCurrent(current);
        }
        catch { ProtocolStorageAmbient.Current?.Request.Fail(); throw; }
    }

    /// <summary>Preserves exact context/claims and forbids storage/reference behavior or fixed-identity substitution.</summary>
    private void RequireCurrent(OpenIddictClientEvents.GenerateTokenContext current)
    {
        preparation.RequireCurrent(current.SecurityTokenDescriptor, current.SecurityTokenHandler);
        var created = principal.GetCreationDate();
        var expires = principal.GetExpirationDate();
        var identifier = principal.GetClaim(OpenIddictConstants.Claims.JwtId);
        var assertion = created is { } start && expires is { } end && identifier is not null
            ? AdministratorAssertion.Create(identifier, start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds(), null) : null;
        if (!ReferenceEquals(current, context) || !ReferenceEquals(current.Principal, principal) ||
            current.Registration.RegistrationId != AdministratorProtocolIdentity.ClientId ||
            current.Registration.ClientId != AdministratorProtocolIdentity.ClientId || current.Registration.Issuer?.OriginalString != profile.Issuer.AbsoluteUri ||
            current.Registration.SigningCredentials.Count != 1 || !profile.Registration.Key.MatchesValidatedKey(current.Registration.SigningCredentials[0].Key) ||
            claims != ProtocolTokenDescriptorSnapshot.Claims(principal) || current.CreateTokenEntry || current.PersistTokenPayload || current.IsReferenceToken ||
            current.TokenFormat != OpenIddictConstants.TokenFormats.Private.JsonWebToken || current.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion ||
            current.IsRejected || current.IsRequestHandled || current.IsRequestSkipped ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.Issuer) != AdministratorProtocolIdentity.ClientId ||
            principal.GetClaim(OpenIddictConstants.Claims.Subject) != AdministratorProtocolIdentity.ClientId ||
            !principal.GetAudiences().SequenceEqual([profile.Issuer.AbsoluteUri], StringComparer.Ordinal) ||
            assertion is null || !assertion.Allows(current.Options.TimeProvider!.GetUtcNow().ToUnixTimeSeconds()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
