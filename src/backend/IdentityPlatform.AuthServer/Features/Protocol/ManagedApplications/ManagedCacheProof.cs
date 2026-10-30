using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Owns exactly one anonymous cached-request generation and its actual native self-redirect under one write epoch.</summary>
internal sealed class ManagedCacheProof
{
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStoragePhase phase;
    private readonly ProtocolStorageRequest request;
    private readonly OpenIddictServerEvents.ProcessSignInContext signIn;
    private readonly ManagedCacheResponse response;
    private NativeIssuedToken? entry;
    private bool generating;

    /// <summary>Creates a separate managed observation capability, never an administrator issuance permit.</summary>
    internal ManagedCacheProof(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext signIn)
    {
        this.scope = scope;
        this.signIn = signIn;
        phase = scope.Owner.Capture(write: true);
        request = ProtocolStorageAmbient.Current!.Request;
        response = new(signIn);
        if (Find(signIn.Transaction) is not null || NativeIssuanceProof.Find(signIn.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        signIn.Transaction.SetProperty(typeof(ManagedCacheProof).FullName!, this);
    }

    /// <summary>Resolves only the privately attached cache observation in this native transaction.</summary>
    internal static ManagedCacheProof? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<ManagedCacheProof>(typeof(ManagedCacheProof).FullName!);

    /// <summary>Observes precisely one genuine native reference-request generation; nested or additional artifacts fail closed.</summary>
    internal async ValueTask GenerateAsync(OpenIddictServerEvents.GenerateTokenContext context, Func<ValueTask> native)
    {
        RequireCurrent();
        if (entry is not null || generating || !ReferenceEquals(context.Transaction, signIn.Transaction) ||
            context.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken || context.ClientId != signIn.ClientId)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        entry = new(context, signIn.ClientId!);
        generating = true;
        try
        {
            await native();
            RequireCurrent();
            if (!context.IsRejected)
            {
                entry.Complete();
            }
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
        finally { generating = false; }
    }

    /// <summary>Delegates actual native preparation/create/sign/persist stages through the existing low-level observation mechanism.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext context, Func<NativeIssuedToken, ValueTask> operation)
    {
        try
        {
            RequireCurrent();
            if (!generating || entry is null)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            entry.RequireContext(context);
            await operation(entry);
            RequireCurrent();
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
    }

    /// <summary>Observes the actual installed self-redirect formatter, distinct from authorization-code query responses.</summary>
    internal async ValueTask FormatAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context, Func<ValueTask> native)
    {
        try
        {
            RequireCurrent();
            await response.ObserveAsync(context, native);
            RequireCurrent();
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
    }

    /// <summary>Requires native persistence, reference resolution and frozen formatter output after all response callbacks have completed.</summary>
    internal async ValueTask<bool> VerifyAsync(string application, CancellationToken token)
    {
        RequireCurrent();
        return !generating && entry is not null && response.Current() && entry.Matches(signIn.RequestTokenPrincipal, signIn.RequestToken) &&
            await entry.VerifyAsync(scope, scope.Services.GetRequiredService<IOpenIddictTokenManager>(), application, token) && response.Current();
    }

    /// <summary>Rejects a disposed, foreign or mutated native/HTTP epoch even when an exception was caught by another handler.</summary>
    private void RequireCurrent()
    {
        request.RequireCurrent();
        scope.Owner.Check(phase, signIn.CancellationToken);
        if (!ReferenceEquals(ProtocolStorageAmbient.Current?.Request, request) || !ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, scope))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
