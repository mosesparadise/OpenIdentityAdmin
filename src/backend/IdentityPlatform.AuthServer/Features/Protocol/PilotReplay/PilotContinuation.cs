using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Binds durable consumption to the exact protected input and native sign-in continuation.</summary>
internal sealed class PilotContinuation
{
    private readonly PilotExchange exchange;
    private readonly OpenIddictRequest request;
    private readonly OpenIddictServerEvents.ProcessAuthenticationContext authentication;
    private readonly string claims;
    private readonly string parameters;
    internal OpenIddictServerEvents.ProcessSignInContext Context
    {
        get;
    }
    internal ClaimsPrincipal Principal
    {
        get;
    }
    internal DateTimeOffset? Expiration
    {
        get; private set;
    }
    internal bool Consumed
    {
        get; private set;
    }

    /// <summary>Freezes the original native authenticated code without reconstructing an authentication result.</summary>
    internal PilotContinuation(PilotExchange exchange, OpenIddictServerEvents.ProcessSignInContext context)
    {
        this.exchange = exchange;
        Context = context;
        request = context.Request;
        authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!) ?? throw ProtocolStorageOwner.Unavailable();
        var original = authentication.AuthorizationCodePrincipal ?? throw ProtocolStorageOwner.Unavailable();
        Principal = new ClaimsPrincipal(original.Identities.Select(identity => new ClaimsIdentity(identity)));
        claims = Binding(original);
        parameters = Parameters(request);
        RequireCurrent();
    }

    /// <summary>Requires original HTTP, request, authentication, claims and code-subject identities on every continuation.</summary>
    internal void RequireCurrent()
    {
        exchange.Request.RequireCurrent();
        if (!ReferenceEquals(PilotExchange.Current, exchange) || !ReferenceEquals(Context.Request, request) ||
            !ReferenceEquals(authentication, Context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
                typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!)) ||
            authentication.AuthorizationCodePrincipal is not { } original || claims != Binding(original) || parameters != Parameters(request) ||
            !PilotExchange.Candidate(Context) || !OutputMatches())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Preserves protected issuance authority while permitting native transport-property normalization on the sign-in principal.</summary>
    private bool OutputMatches() => Context.Principal is { } output &&
        output.GetClaim(OpenIddictConstants.Claims.Subject) == Principal.GetClaim(OpenIddictConstants.Claims.Subject) &&
        output.GetAuthorizationId() == Principal.GetAuthorizationId() && output.GetScopes().SequenceEqual(Principal.GetScopes(), StringComparer.Ordinal) &&
        output.GetResources().SequenceEqual(Principal.GetResources(), StringComparer.Ordinal) && output.GetPresenters().SequenceEqual(Principal.GetPresenters(), StringComparer.Ordinal);

    /// <summary>Retains the original persisted expiry before consumption; no continuation can extend it.</summary>
    internal bool BindExpiration(DateTimeOffset? expiration)
    {
        if (expiration is null || Expiration is not null && Expiration != expiration)
        {
            return false;
        }
        Expiration = expiration;
        return true;
    }

    /// <summary>Records durable consumption only after the child commit and its actual scope disposal have returned.</summary>
    internal void CompleteConsumption()
    {
        RequireCurrent();
        if (Consumed || Context.IsRejected || Context.IsRequestHandled || Context.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        Consumed = true;
    }

    /// <summary>Reuses the protected original principal for lifecycle checks without repeating authentication or replaying consumed validation.</summary>
    internal OpenIddictServerEvents.ValidateTokenContext Validation(CancellationToken token) => new(Context.Transaction)
    {
        Principal = Principal,
        TokenId = Principal.GetTokenId(),
        AuthorizationId = Principal.GetAuthorizationId(),
        CancellationToken = token
    };

    /// <summary>Hashes every protected claim and destination property without retaining printable protocol credentials.</summary>
    private static string Binding(ClaimsPrincipal principal) => FactorBinding.Digest(principal.Claims.Select(claim => new
    {
        claim.Type,
        claim.Value,
        claim.ValueType,
        claim.Issuer,
        claim.OriginalIssuer,
        Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
    }).ToArray());

    /// <summary>Freezes all extracted native parameters so proof and redirect bindings cannot be substituted after validation.</summary>
    private static string Parameters(OpenIddictRequest request) => FactorBinding.Digest(request.GetParameters()
        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());
}
