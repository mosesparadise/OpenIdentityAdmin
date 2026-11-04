using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Observes exact managed native authorization, token cryptography and formatting under a separate issuance capability.</summary>
internal sealed class ManagedIssuanceProof
{
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStoragePhase phase;
    private readonly OpenIddictServerEvents.ProcessSignInContext signIn;
    private readonly NativeIssuedAuthorization authorization;
    private readonly NativeResponseCompletion response;
    private readonly HashSet<string> expected;
    private readonly Dictionary<string, NativeIssuedToken> tokens = new(StringComparer.Ordinal);
    private NativeIssuedToken? active;
    private readonly Action<ClaimsPrincipal, string> requirePrepared;

    /// <summary>Begins only after exact quota reservation inside the owned managed issuance transaction.</summary>
    internal ManagedIssuanceProof(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext signIn, string callback, Action<ClaimsPrincipal, string> requirePrepared)
    {
        this.scope = scope;
        this.signIn = signIn;
        this.requirePrepared = requirePrepared;
        phase = scope.Owner.Capture(write: true);
        authorization = new(scope, signIn);
        response = new(signIn, callback, ManagedResponseRules.Allows);
        expected = signIn.EndpointType == OpenIddictServerEndpointType.Authorization
            ? [OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode]
            : [OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken];
        if (Find(signIn.Transaction) is not null || NativeIssuanceProof.Find(signIn.Transaction) is not null || ManagedCacheProof.Find(signIn.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        signIn.Transaction.SetProperty(typeof(ManagedIssuanceProof).FullName!, this);
    }

    /// <summary>Finds only a private managed issuance observation installed after reservation.</summary>
    internal static ManagedIssuanceProof? Find(OpenIddictServerTransaction transaction) => transaction.GetProperty<ManagedIssuanceProof>(typeof(ManagedIssuanceProof).FullName!);

    /// <summary>Observes one real native authorization attachment under the same exact write epoch.</summary>
    internal ValueTask AuthorizationAsync(Func<ValueTask> native) => GuardAsync(() => authorization.ObserveAsync(native));

    /// <summary>Requires each planned native generator exactly once and reuses actual creation/cryptography/payload witnesses.</summary>
    internal async ValueTask GenerateAsync(OpenIddictServerEvents.GenerateTokenContext context, Func<ValueTask> native)
    {
        Require();
        if (active is not null || !ReferenceEquals(context.Transaction, signIn.Transaction) || !expected.Contains(context.TokenType) || tokens.ContainsKey(context.TokenType))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        requirePrepared(context.Principal, context.TokenType);
        var entry = new NativeIssuedToken(context, signIn.ClientId!);
        tokens.Add(context.TokenType, entry);
        active = entry;
        try
        {
            await GuardAsync(native);
            if (!context.IsRejected)
            {
                entry.Complete();
            }
        }
        finally { active = null; }
    }

    /// <summary>Preserves native stages and refuses foreign or nested generation observations.</summary>
    internal ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext context, Func<NativeIssuedToken, ValueTask> operation) => GuardAsync(async () =>
    {
        if (active is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        active.RequireContext(context);
        await operation(active);
    });

    /// <summary>Observes the actual native query/JSON formatter without sharing administrator publication permits.</summary>
    internal ValueTask FormatAsync(OpenIddictServerEvents.BaseRequestContext context, Func<ValueTask> native) => GuardAsync(() => response.ObserveAsync(context, native));

    /// <summary>Checks exact created rows, prepared principals and final native output after response callbacks finish.</summary>
    internal async ValueTask<bool> VerifyAsync(string application, CancellationToken token)
    {
        Require();
        if (active is not null || !expected.SetEquals(tokens.Keys) || !response.IsCurrent() || !ManagedResponseRules.Allows(signIn) ||
            !await authorization.VerifyAsync(application, token))
        {
            return false;
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        foreach (var (type, entry) in tokens)
        {
            requirePrepared(type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode ? signIn.AuthorizationCodePrincipal! :
                type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken ? signIn.AccessTokenPrincipal! : signIn.IdentityTokenPrincipal!, type);
            var matches = type switch
            {
                OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode => entry.Matches(signIn.AuthorizationCodePrincipal, signIn.AuthorizationCode),
                OpenIddictConstants.TokenTypeIdentifiers.AccessToken => entry.Matches(signIn.AccessTokenPrincipal, signIn.AccessToken),
                OpenIddictConstants.TokenTypeIdentifiers.IdentityToken => entry.Matches(signIn.IdentityTokenPrincipal, signIn.IdentityToken),
                _ => false
            };
            if (!matches || !await entry.VerifyAsync(scope, manager, application, token))
            {
                return false;
            }
        }
        Require();
        return authorization.IsBound() && response.IsCurrent();
    }

    /// <summary>Latches native failures even if a later handler catches their exception.</summary>
    private async ValueTask GuardAsync(Func<ValueTask> operation)
    {
        try
        {
            Require();
            await operation();
            Require();
        }
        catch { ProtocolStorageAmbient.Current?.Request.Fail(); scope.Owner.Fail(); throw; }
    }

    /// <summary>Rejects ended, foreign or substituted ownership at every asynchronous native boundary.</summary>
    private void Require()
    {
        scope.Owner.Check(phase, signIn.CancellationToken);
        if (!ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ProtocolStorageAmbient.Current!.Request.RequireCurrent();
    }
}
