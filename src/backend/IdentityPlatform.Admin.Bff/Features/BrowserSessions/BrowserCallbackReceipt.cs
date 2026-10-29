using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Requires one exact product callback across native consumption, transport, identity validation and final publication.</summary>
internal sealed class BrowserCallbackReceipt(HttpContext http, IServiceScopeFactory factory, TimeProvider clock)
{
    private OpenIddictClientEvents.ProcessAuthenticationContext? original;
    private BrowserCallbackAdmission? admission;
    private BrowserSessionIdentity? identity;
    private string? identityToken;
    private string? accessToken;
    private BrowserAccessCredential? access;
    private ClaimsPrincipal? identityPrincipal;
    private string? identityClaims;
    private int admitted;
    private int captured;

    /// <summary>Reads browser ownership only after native redemption, retaining consumed-state behavior for substituted product proofs.</summary>
    internal async ValueTask AdmitAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        if (!Belongs(context) || Interlocked.Exchange(ref admitted, 1) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        NativeClientCallback.Find(context).Require(context);
        original = context;
        admission = await BrowserCallbackAdmission.ReadAsync(http, factory, clock);
        RequireTransport(context);
    }

    /// <summary>Freezes only the exact natively validated backchannel identity, never the merged application principal.</summary>
    internal void Capture(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        RequireTransport(context);
        if (Interlocked.Exchange(ref captured, 1) != 0 || context.BackchannelIdentityTokenPrincipal is not { } principal ||
            context.BackchannelIdentityToken is not { Length: > 0 } encoded || context.BackchannelAccessToken is not { Length: > 0 } access ||
            BrowserSessionIdentity.Validate(principal, encoded, admission!.Pending.Digest, clock.GetUtcNow()) is not { } validated ||
            BrowserAccessCredential.Capture(access, context.BackchannelAccessTokenExpirationDate, clock.GetUtcNow()) is not { } credential)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        identity = validated;
        identityToken = encoded;
        accessToken = access;
        this.access = credential;
        identityPrincipal = principal;
        identityClaims = NativeStateBinding.Claims(principal);
    }

    /// <summary>Rechecks original native receipt and product bounds immediately around actual transport; removed admission cannot downgrade the callback.</summary>
    internal void RequireTransport(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        if (!Belongs(context) || !ReferenceEquals(original, context) || admission is null || context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped ||
            context.StateTokenPrincipal is not { } state || !BrowserCallbackState.Matches(state, admission.Browser.Digest,
                admission.Pending.Digest, admission.Pending.Generation, admission.Reservation))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        NativeClientCallback.Find(context).Require(context);
        admission.Publication.Require(clock);
    }

    /// <summary>Revalidates immutable signed identity values, native lifetime and all original browser bounds after every cleanup boundary.</summary>
    internal void Require()
    {
        var context = original ?? throw ProtocolStorageOwner.Unavailable();
        RequireTransport(context);
        if (captured != 1 || identity is null || access is null || identityToken != context.BackchannelIdentityToken || accessToken != context.BackchannelAccessToken ||
            access.ExpiresAt != context.BackchannelAccessTokenExpirationDate || access.ExpiresAt <= clock.GetUtcNow() ||
            !ReferenceEquals(identityPrincipal, context.BackchannelIdentityTokenPrincipal) || identityClaims != NativeStateBinding.Claims(identityPrincipal!) ||
            !NativeClientTokenTime.Current(context.BackchannelIdentityTokenPrincipal, clock) ||
            !NativeBrowserIdentity.Matches(context))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Returns the exact captured identity, access credential and reservation after full native authentication succeeds.</summary>
    internal (BrowserCallbackAdmission Admission, BrowserSessionIdentity Identity, BrowserAccessCredential Access) Completed()
    {
        Require();
        return (admission!, identity!, access!);
    }

    /// <summary>Requires the route's original feature and native transaction, preventing replacement or reuse on another request.</summary>
    private bool Belongs(OpenIddictClientEvents.ProcessAuthenticationContext context) =>
        ReferenceEquals(context.Transaction.GetHttpRequest()?.HttpContext, http) &&
        ReferenceEquals(http.Features.Get<BrowserCallbackReceipt>(), this) &&
        ReferenceEquals(http.Features.Get<OpenIddictClientAspNetCoreFeature>()?.Transaction, context.Transaction);

    /// <summary>Preserves ordinary B2 when no product callback is demanded; a demanded callback must have completed admission.</summary>
    internal static void DemandTransport(OpenIddictClientEvents.ProcessAuthenticationContext context) =>
        context.Transaction.GetHttpRequest()?.HttpContext.Features.Get<BrowserCallbackReceipt>()?.RequireTransport(context);
}

/// <summary>Checks product browser ownership after native state consumption and before any token transport.</summary>
internal sealed class BrowserCallbackAdmissionHandler : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Only an explicitly owned callback can demand this additional product admission.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context) =>
        context.Transaction.GetHttpRequest()?.HttpContext.Features.Get<BrowserCallbackReceipt>()?.AdmitAsync(context) ?? ValueTask.CompletedTask;
}

/// <summary>Captures product identity after existing native and signed B3 validation, before merged-principal projection.</summary>
internal sealed class BrowserCallbackIdentityHandler : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Supplies no generic authentication fallback when a product callback lacks its earlier admission.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        context.Transaction.GetHttpRequest()?.HttpContext.Features.Get<BrowserCallbackReceipt>()?.Capture(context);
        return ValueTask.CompletedTask;
    }
}
