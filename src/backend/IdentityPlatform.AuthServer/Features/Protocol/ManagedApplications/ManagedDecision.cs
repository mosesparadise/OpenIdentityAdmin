using System.Globalization;
using IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Validates a restored managed request without reusing issuer cookies or granting incomplete password/MFA/consent authority.</summary>
internal static class ManagedDecision
{
    private const string SetCookie = "Set-Cookie";

    /// <summary>Requires actual native endpoint handoff, then checks current request/browser/policy in a fresh serialized child.</summary>
    internal static async Task<IResult> AuthorizeAsync(HttpContext http, ManagedAdmission admission)
    {
        var principal = RequireNative(http, admission, out var outer, out var browser);
        var result = await ProtocolResponseChild.RunAsync(http, http.RequestServices.GetRequiredService<IServiceScopeFactory>(), async (scope, response, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await ManagedRegistration.LockAsync(scope, token);
            var registration = await ManagedRegistration.ReadAsync(scope, admission.Demand.ClientId, token);
            if (!admission.Allows(outer.Transaction, registration) ||
                principal.GetClaim(ManagedDemand.RevisionClaim) != registration!.Revision.ToString(CultureInfo.InvariantCulture))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            registration = await registration!.ObserveAsync(scope, token);
            var row = await RequireInteractionAsync(scope, admission, registration, principal.GetClaim(ManagedDemand.SelectorClaim)!, browser,
                principal.GetTokenId()!, principal.GetExpirationDate()?.ToUnixTimeSeconds(), token);
            ProtocolStorageAmbient.Current!.Request.RestrictPublication(DateTimeOffset.FromUnixTimeSeconds(Math.Min(row.ExpiresAt, registration.ExpiresAt)));
            var form = CreateForm(admission, outer, principal, row);
            var location = QueryHelpers.AddQueryString(ManagedFormProof.Path, ManagedFormProof.Field, ManagedFormProof.Protect(http, form));
            await Results.LocalRedirect(location).ExecuteAsync(http);
            var expected = FactorBinding.Digest(row);
            await response.SealAsync();
            var persisted = await ManagedHumanBinding.RowAsync(scope, form, token);
            if (expected != FactorBinding.Digest(persisted) || !registration.Matches(await ManagedHumanBinding.NativeAsync(scope, form, persisted, token)))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            if (http.Response.StatusCode != StatusCodes.Status302Found || http.Response.Headers.Location != location || http.Response.Headers.ContainsKey(SetCookie))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            if (ManagedLoginRules.Observe(scope.Owner.Clock, Math.Max(row.ObservedAt, registration.ObservedAt)) >= Math.Min(row.ExpiresAt, registration.ExpiresAt))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            await transaction.CommitAsync(token);
            return row.ExpiresAt;
        });
        await ManagedEntryPublication.PrepareAsync(http, outer, admission, result.Output ?? throw ProtocolStorageOwner.Unavailable(), result.Result);
        return Results.Empty;
    }

    /// <summary>Captures only the server-restored native intent and immutable managed-row authority for the new protected form.</summary>
    private static ManagedHumanRequest CreateForm(ManagedAdmission admission, OpenIddictServerEvents.ProcessRequestContext outer,
        System.Security.Claims.ClaimsPrincipal principal, ManagedLoginInteraction row) => new()
        {
            Selector = principal.GetClaim(ManagedDemand.SelectorClaim)!,
            BrowserDigest = row.BrowserDigest,
            ClientId = admission.Demand.ClientId,
            Callback = admission.Demand.Callback,
            DemandDigest = row.DemandDigest,
            RequestId = row.RequestId!,
            RequestUri = outer.Transaction.Request!.RequestUri!,
            Correlation = CeremonySecrets.Create(),
            ExpiresAt = row.ExpiresAt,
            Revision = row.Revision,
            State = row.State
        };

    /// <summary>Requires the real restored native request and browser proof before allocating an account-page continuation.</summary>
    private static System.Security.Claims.ClaimsPrincipal RequireNative(HttpContext http, ManagedAdmission admission,
        out OpenIddictServerEvents.ProcessRequestContext outer, out string browser)
    {
        outer = http.RequestServices.GetRequiredService<ProtocolHostRequest>().Outer ?? throw ProtocolStorageOwner.Unavailable();
        http.RequestServices.GetRequiredService<ProtocolHostRequest>().RequireInferred(outer);
        var authentication = outer.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var principal = authentication?.RequestTokenPrincipal;
        browser = ManagedBrowser.Read(http)!;
        if (!admission.HasHandoff || !outer.IsRequestSkipped || outer.IsRequestHandled || outer.IsRejected || !admission.Demand.Cached ||
            authentication is null || authentication.IsRejected || authentication.IsRequestHandled || authentication.IsRequestSkipped ||
            principal is null || browser is null || !principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken) ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) != OpenIddictConstants.RequestTokenTypes.Private.CachedAuthorizationRequest ||
            !principal.GetPresenters().SequenceEqual([admission.Demand.ClientId], StringComparer.Ordinal) ||
            !ManagedLoginRules.IsSecret(principal.GetClaim(ManagedDemand.SelectorClaim)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return principal;
    }

    /// <summary>Rejects foreign, detached, expired, terminal or edited interactions without renewing the original deadline or consuming the native request.</summary>
    private static async ValueTask<ManagedLoginInteraction> RequireInteractionAsync(ProtocolStorageScope scope, ManagedAdmission admission, ManagedRegistration registration,
        string selector, string browser, string requestId, long? expiration, CancellationToken token)
    {
        var digest = ManagedLoginRules.Digest(selector);
        var row = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction?>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.SelectorDigest == digest, current)), token);
        if (row is null || row.SelectorDigest != digest || row.BrowserDigest != ManagedLoginRules.Digest(browser) ||
            row.ApplicationId != registration.ApplicationId || row.ApplicationRevision != registration.Revision || row.RequestId != requestId ||
            row.RequestExpiresAt != expiration || row.ExpiresAt != expiration || row.DemandDigest != admission.Demand.Digest ||
            row.State != ManagedLoginState.PendingPassword || ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt) >= row.ExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(requestId, token);
        if (entry is null || !await ProtocolStorageRows.ReloadAsync(scope, entry, token) || await manager.GetIdAsync(entry, token) != requestId ||
            await manager.GetApplicationIdAsync(entry, token) != registration.ApplicationId ||
            await manager.GetTypeAsync(entry, token) != OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken ||
            await manager.GetStatusAsync(entry, token) != OpenIddictConstants.Statuses.Valid ||
            (await manager.GetExpirationDateAsync(entry, token))?.ToUnixTimeSeconds() != expiration ||
            ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt) >= row.ExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt);
        var changed = await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .Where(value => value.SelectorDigest == digest && value.Revision == row.Revision && value.ObservedAt == row.ObservedAt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ObservedAt, now), current)), token, write: true);
        if (changed != 1)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        row.ObservedAt = now;
        return row;
    }
}
