using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Transfers one committed local reservation to one exact native challenge without exposing browser-supplied authentication properties.</summary>
internal sealed class BrowserLoginReservation
{
    internal const string BrowserClaim = "oi_b3_browser";
    internal const string GenerationClaim = "oi_b3_generation";
    internal const string ReservationClaim = "oi_b3_reservation";
    private static readonly string[] Names = [BrowserClaim, GenerationClaim, ReservationClaim];
    private readonly HttpContext request;
    private readonly BrowserPendingProof pending;
    private readonly BrowserLoginBinding binding;
    private readonly string reservation;
    private readonly Guid correlation;
    private OpenIddictClientEvents.ProcessChallengeContext? challenge;
    private int claimed;

    /// <summary>Construction follows the acknowledged reservation commit and transaction disposal only.</summary>
    private BrowserLoginReservation(HttpContext request, BrowserPendingProof pending, BrowserLoginBinding binding, string reservation, Guid correlation)
    {
        this.request = request;
        this.pending = pending;
        this.binding = binding;
        this.reservation = reservation;
        this.correlation = correlation;
    }

    /// <summary>Spends the original pending generation once; unknown commit or denial returns no capability and never retries.</summary>
    internal static async Task<BrowserLoginReservation?> ReserveAsync(HttpContext request, BrowserPendingProof pending, string completion,
        BrowserLoginCompletion coordinator)
    {
        var fields = new OpenIddictRequest();
        fields.SetParameter(BrowserLoginBinding.IntentParameter, pending.Intent);
        fields.SetParameter(BrowserLoginBinding.CompletionParameter, completion);
        if (BrowserLoginBinding.Read(fields, out var binding) != BrowserLoginBindingState.Present)
        {
            return null;
        }
        var reservation = BrowserLoginSelector.Digest(BrowserLoginSelector.CreateSelector())!;
        var correlation = await coordinator.ReserveCorrelationAsync(pending.BrowserDigest, pending.Digest, pending.Generation, reservation, request.RequestAborted);
        return correlation is { } committed ? new(request, pending, binding!, reservation, committed) : null;
    }

    /// <summary>Consumes the private HTTP capability before the existing native challenge guard and state preparation.</summary>
    internal void Attach(OpenIddictClientEvents.ProcessChallengeContext context, NativeClientProfile profile)
    {
        if (!ReferenceEquals(request, context.Transaction.GetHttpRequest()?.HttpContext) || Interlocked.Exchange(ref claimed, 1) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        challenge = context;
        if (!Current(context))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        NativeBrowserChallenge.Attach(context, profile, binding, new DateTimeOffset(pending.Expires, TimeSpan.Zero), correlation, this);
    }

    /// <summary>Rechecks original receipt identity and deadline without treating retained selectors as reusable authority.</summary>
    internal bool Current(OpenIddictClientEvents.ProcessChallengeContext context) => ReferenceEquals(challenge, context) &&
        ReferenceEquals(request, context.Transaction.GetHttpRequest()?.HttpContext) && context.TargetLinkUri is null &&
        BrowserSessionPolicy.Current(pending.Created, pending.Created, pending.Expires, context.Options.TimeProvider!.GetUtcNow().UtcTicks);

    /// <summary>Rejects a missing capture handler before state creation when a product request demands an owned reservation.</summary>
    internal static bool DemandMatches(OpenIddictClientEvents.ProcessChallengeContext context) =>
        context.Transaction.GetHttpRequest()?.HttpContext.Features.Get<BrowserLoginReservation>() is not { } reservation || reservation.Current(context);

    /// <summary>Requires the original route receipt after native work; losing a feature or handler cannot publish an ordinary unbound challenge.</summary>
    internal void RequireCompleted(HttpContext http)
    {
        if (!ReferenceEquals(http, request) || !ReferenceEquals(http.Features.Get<BrowserLoginReservation>(), this) || claimed != 1 ||
            challenge is not { IsRejected: false, IsRequestSkipped: false } current || !Current(current) ||
            NativeBrowserChallenge.Find(current) is not { } captured || !captured.StateMatches(current))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Protects only the reserved durable tuple; raw browser/intent/completion selectors stay out of native state.</summary>
    internal void Project(ClaimsPrincipal state)
    {
        if (!Absent(state))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        state.SetClaim(BrowserClaim, pending.BrowserDigest).SetClaim(GenerationClaim, pending.Generation).SetClaim(ReservationClaim, reservation);
    }

    /// <summary>Rejects pre-persistence substitution and duplicate/aliased native claims even before the existing frozen-state owner runs.</summary>
    internal bool Matches(ClaimsPrincipal state) => state.GetClaim(OpenIddictConstants.Claims.TargetLinkUri) is null &&
        Match(state, BrowserClaim, pending.BrowserDigest, ClaimValueTypes.String) &&
        Match(state, GenerationClaim, pending.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64) &&
        Match(state, ReservationClaim, reservation, ClaimValueTypes.String);

    /// <summary>Prevents ordinary or unreserved challenge callers from injecting product establishment provenance.</summary>
    internal static bool Absent(ClaimsPrincipal state) => !state.Claims.Any(claim => Names.Any(name =>
        string.Equals(name, claim.Type, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Requires exact scalar type/value/cardinality with no token destination on private reservation claims.</summary>
    private static bool Match(ClaimsPrincipal state, string name, string value, string type)
    {
        var claims = state.Claims.Where(claim => string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return claims is [var claim] && claim.Type == name && claim.Value == value && claim.ValueType == type && claim.GetDestinations().IsEmpty;
    }
}

/// <summary>Accepts a route-created reservation only from the exact active HTTP request.</summary>
internal sealed class BrowserLoginChallenge(NativeClientProfile profile) : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessChallengeContext>
{
    /// <summary>Runs before native profile enforcement; absence preserves ordinary B2 and prerequisite T009 fixtures.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        context.Transaction.GetHttpRequest()?.HttpContext.Features.Get<BrowserLoginReservation>()?.Attach(context, profile);
        return ValueTask.CompletedTask;
    }
}
