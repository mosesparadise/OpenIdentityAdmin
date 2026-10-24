using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Verifies terminal attached completion under existing account/profile ownership without reversing ceremony lock order.</summary>
internal sealed class BrowserIssuanceAuthority(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context,
    AdministratorDecision decision)
{
    private BrowserLoginBinding? binding;
    private string? origin;
    private string? provenance;
    private long expires;
    private long sourceExpires;
    private IssuerContinuation? observation;

    /// <summary>Matches protected native input to the completed ceremony and current source authority before projecting any browser claims.</summary>
    internal async ValueTask<bool> OpenAsync(AdministratorRegistrationPolicy policy, ProtocolAdmissionClaims current, CancellationToken token)
    {
        var state = BrowserLoginBinding.Read(decision.Original, out binding);
        if (state == BrowserLoginBindingState.Absent)
        {
            return BrowserIssuanceClaims.PrivateAbsent(decision.Original) && BrowserIssuanceClaims.PublicAbsent(decision.Original) &&
                BrowserLoginBinding.Read(context.Request, out _) == BrowserLoginBindingState.Absent;
        }
        if (state != BrowserLoginBindingState.Present || !policy.IsBrowserSession || !BrowserIssuanceClaims.PublicAbsent(decision.Original))
        {
            return false;
        }
        origin = context.EndpointType == OpenIddictServerEndpointType.Authorization ? decision.Original.GetTokenId() : BrowserIssuanceClaims.Origin(decision.Original);
        if (string.IsNullOrEmpty(origin) || !OriginalMatches())
        {
            return false;
        }
        var row = await ReadAsync(token);
        if (row is null || !Matches(row, current))
        {
            return false;
        }
        provenance = FactorBinding.Digest(row);
        expires = row.ExpiresAt;
        sourceExpires = current.SourceExpiresAt;
        return true;
    }

    /// <summary>Keeps code provenance private; only a bound exchange receives the two ID-token destinations.</summary>
    internal void Project(ClaimsPrincipal principal)
    {
        if (binding is null)
        {
            return;
        }
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization)
        {
            binding.CopyTo(principal);
            principal.SetClaim(BrowserIssuanceClaims.RequestId, origin);
        }
        else
        {
            BrowserIssuanceClaims.Publish(principal, sourceExpires, binding.IntentDigest);
        }
    }

    /// <summary>Preserves every shorter original intent deadline independently of the authenticated source-session deadline.</summary>
    internal long Bound(long maximum) => binding is null ? maximum : Math.Min(maximum, expires);

    /// <summary>Freshly reads immutable terminal provenance after awaited native work and checks the actual prepared output principals.</summary>
    internal async ValueTask RefreshAsync(CancellationToken token)
    {
        observation = binding is null ? null : await ReadAsync(token);
    }

    /// <summary>Runs after the last source-authority await, so no later database read can hide freshness or original-intent expiry.</summary>
    internal bool IsCurrent(ProtocolAdmissionClaims current)
    {
        if (context.Principal is null)
        {
            return false;
        }
        if (binding is null)
        {
            return BrowserIssuanceClaims.PrivateAbsent(decision.Original) && BrowserIssuanceClaims.PublicAbsent(decision.Original) && BrowserIssuanceClaims.PrivateAbsent(context.Principal) &&
                OutputsAbsent() && BrowserIssuanceClaims.PublicAbsent(context.Principal);
        }
        var row = observation;
        return row is not null && Matches(row, current) && FactorBinding.Digest(row) == provenance && OriginalMatches() &&
            BrowserIssuanceClaims.PrivateMatches(context.Principal, binding, origin!) && PreparedMatches();
    }

    /// <summary>Reads detached terminal state inside the already-owned transaction; no admission lock or observation mutation is introduced.</summary>
    private ValueTask<IssuerContinuation?> ReadAsync(CancellationToken token) => scope.Owner.RunAsync(cancellation =>
        new ValueTask<IssuerContinuation?>(((PlatformDbContext)scope.Owner.Database).Set<IssuerContinuation>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.IntentDigest == binding!.IntentDigest, cancellation)), token);

    /// <summary>Requires exact live ceremony/session provenance and retention, not just possession of an opaque completion selector.</summary>
    private bool Matches(IssuerContinuation row, ProtocolAdmissionClaims current) =>
        row.State == IssuerContinuationPolicy.Attached && row.RequestId == origin && row.ClientId == AdministratorProtocolIdentity.ClientId &&
        row.CompletionDigest == IssuerContinuationPolicy.Digest(binding!.Completion) && row.UserId == current.UserId &&
        row.SessionId == current.SessionId && row.SessionExpiresAt == current.SourceExpiresAt &&
        row.InteractionId is { Length: > 0 } && row.InteractionBrowserDigest is { Length: IssuerContinuationPolicy.DigestLength } &&
        row.BrowserBudgetId is { Length: > 0 } && row.CorrelationId != Guid.Empty &&
        row.RetainUntil >= current.SourceExpiresAt + IssuerContinuationPolicy.ClockToleranceSeconds &&
        IssuerContinuationPolicy.IsCurrent(row, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());

    /// <summary>Rechecks protected input and restored request independently of mutable sign-in projections.</summary>
    private bool OriginalMatches() => binding!.Matches(decision.Original) && BrowserIssuanceClaims.PublicAbsent(decision.Original) &&
        (context.EndpointType == OpenIddictServerEndpointType.Authorization
            ? BrowserIssuanceClaims.OriginAbsent(decision.Original) && decision.Original.GetTokenId() == origin && BrowserLoginBinding.Read(context.Request, out var request) == BrowserLoginBindingState.Present &&
                request!.Intent == binding.Intent && request.Completion == binding.Completion
            : BrowserIssuanceClaims.PrivateMatches(decision.Original, binding, origin!) &&
                BrowserLoginBinding.Read(context.Request, out _) == BrowserLoginBindingState.Absent);

    /// <summary>Checks native code cloning and ID/access destination isolation before native issuance may commit.</summary>
    private bool PreparedMatches() => context.EndpointType == OpenIddictServerEndpointType.Authorization
        ? context.AuthorizationCodePrincipal is { } code && BrowserIssuanceClaims.PrivateMatches(code, binding!, origin!) && BrowserIssuanceClaims.PublicAbsent(code)
        : context.IdentityTokenPrincipal is { } identity && context.AccessTokenPrincipal is { } access &&
            BrowserIssuanceClaims.PublicMatches(context.Principal!, sourceExpires, binding!.IntentDigest) &&
            BrowserIssuanceClaims.PublicMatches(identity, sourceExpires, binding.IntentDigest) && BrowserIssuanceClaims.PrivateAbsent(identity) &&
            BrowserIssuanceClaims.PublicAbsent(access) && BrowserIssuanceClaims.PrivateAbsent(access);

    /// <summary>Ordinary B2 remains unbound in every actual prepared artifact even when browser composition is installed.</summary>
    private bool OutputsAbsent() => new[] { context.AuthorizationCodePrincipal, context.IdentityTokenPrincipal, context.AccessTokenPrincipal }
        .Where(principal => principal is not null).All(principal => BrowserIssuanceClaims.PrivateAbsent(principal!) && BrowserIssuanceClaims.PublicAbsent(principal!));
}
