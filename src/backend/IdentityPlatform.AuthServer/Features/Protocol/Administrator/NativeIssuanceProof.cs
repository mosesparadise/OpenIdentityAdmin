using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Binds native generation observations to one exact sign-in context and active issuance epoch.</summary>
internal sealed class NativeIssuanceProof
{
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStorageRequest request;
    private readonly ProtocolStoragePhase phase;
    private readonly OpenIddictServerEvents.ProcessSignInContext context;
    private readonly AdministratorIssuancePlan plan;
    private readonly NativeResponseCompletion response;
    private readonly Dictionary<string, NativeIssuedToken> tokens = new(StringComparer.Ordinal);
    private NativeIssuedToken? active;

    /// <summary>Reads only an explicitly attached native issuance observer; ordinary pilot handlers remain unchanged.</summary>
    internal static NativeIssuanceProof? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<NativeIssuanceProof>(typeof(NativeIssuanceProof).FullName!);

    /// <summary>Attaches one observer without allowing a later handler to replace an already active issuance proof.</summary>
    internal void Attach()
    {
        RequireCurrent();
        if (Find(context.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Transaction.SetProperty(typeof(NativeIssuanceProof).FullName!, this);
    }

    /// <summary>Starts observation only inside the fresh write phase after its quota plan has been accepted.</summary>
    internal NativeIssuanceProof(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context, AdministratorIssuancePlan plan, string callback)
    {
        this.scope = scope;
        this.context = context;
        this.plan = plan;
        response = new(context, callback);
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        phase = scope.Owner.Capture(write: true);
        RequireCurrent();
    }

    /// <summary>Observes exactly the expected nested native generations; early handled/skipped paths cannot manufacture completion.</summary>
    internal async ValueTask GenerateAsync(OpenIddictServerEvents.GenerateTokenContext generation, Func<ValueTask> native)
    {
        RequireCurrent();
        if (active is not null || !ReferenceEquals(context.Transaction, generation.Transaction) || !plan.TokenTypes.Contains(generation.TokenType) ||
            tokens.ContainsKey(generation.TokenType))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var entry = new NativeIssuedToken(generation);
        tokens.Add(generation.TokenType, entry);
        active = entry;
        try
        {
            await native();
            RequireCurrent();
            if (!generation.IsRejected)
            {
                entry.Complete();
            }
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
        finally { active = null; }
    }

    /// <summary>Latches any observed native-handler failure so catching it cannot restore issuance authority.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext generation, Func<NativeIssuedToken, ValueTask> operation)
    {
        try
        {
            await operation(Current(generation));
            RequireCurrent();
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
    }

    /// <summary>Records the real native formatter under the same request and epoch, latching any swallowed failure.</summary>
    internal async ValueTask FormatAsync(OpenIddictServerEvents.BaseRequestContext formatting, Func<ValueTask> native)
    {
        try
        {
            RequireCurrent();
            await response.ObserveAsync(formatting, native);
            RequireCurrent();
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
    }

    /// <summary>Exposes only the current exact generation to wrappers around actual native handlers.</summary>
    private NativeIssuedToken Current(OpenIddictServerEvents.GenerateTokenContext generation)
    {
        RequireCurrent();
        if (active is null || !ReferenceEquals(context.Transaction, generation.Transaction))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        active.RequireContext(generation);
        return active;
    }

    /// <summary>Requires every planned native row and final field mapping before commit; formatter completion is a separate obligation.</summary>
    internal async ValueTask<bool> VerifyAsync(string application, CancellationToken token)
    {
        RequireCurrent();
        if (active is not null || tokens.Count != plan.TokenTypes.Count || context.IsRejected || context.IsRequestSkipped ||
            !string.IsNullOrEmpty(context.Response.Error) || !response.IsCurrent() ||
            (context.EndpointType == OpenIddictServerEndpointType.PushedAuthorization && !NativeParResponse.Allows(context.Response)))
        {
            return false;
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        foreach (var pair in tokens)
        {
            if (!Matches(pair.Key, pair.Value) || !await pair.Value.VerifyAsync(scope, manager, application, token))
            {
                return false;
            }
        }
        RequireCurrent();
        return true;
    }

    /// <summary>Checks the library's actual reference prefix and final code/access/identity-token output fields.</summary>
    private bool Matches(string type, NativeIssuedToken entry) => type switch
    {
        OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken => entry.Matches(context.RequestTokenPrincipal, context.RequestToken) &&
            context.Response.RequestUri == OpenIddictConstants.RequestUris.Prefixes.Generic + context.RequestToken,
        OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode => entry.Matches(context.AuthorizationCodePrincipal, context.AuthorizationCode) &&
            context.Response.Code == context.AuthorizationCode,
        OpenIddictConstants.TokenTypeIdentifiers.AccessToken => entry.Matches(context.AccessTokenPrincipal, context.AccessToken) &&
            context.Response.AccessToken == context.AccessToken,
        OpenIddictConstants.TokenTypeIdentifiers.IdentityToken => entry.Matches(context.IdentityTokenPrincipal, context.IdentityToken) &&
            context.Response.IdToken == context.IdentityToken,
        _ => false
    };

    /// <summary>Rejects foreign, completed or disposed ownership while preserving expected native handled status after formatting.</summary>
    private void RequireCurrent()
    {
        request.RequireCurrent();
        scope.Owner.Check(phase, context.CancellationToken);
        if (!ReferenceEquals(ProtocolStorageAmbient.Current?.Request, request) || !ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, scope))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
