using System.Globalization;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Validates a restored managed request without reusing issuer cookies or granting incomplete password/MFA/consent authority.</summary>
internal static class ManagedDecision
{
    /// <summary>Requires actual native endpoint handoff, then checks current request/browser/policy in a fresh serialized child.</summary>
    internal static async Task<IResult> AuthorizeAsync(HttpContext http, ManagedAdmission admission)
    {
        var outer = http.RequestServices.GetRequiredService<ProtocolHostRequest>().Outer ?? throw ProtocolStorageOwner.Unavailable();
        http.RequestServices.GetRequiredService<ProtocolHostRequest>().RequireInferred(outer);
        var authentication = outer.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var principal = authentication?.RequestTokenPrincipal;
        var browser = ManagedBrowser.Read(http);
        if (!admission.HasHandoff || !outer.IsRequestSkipped || outer.IsRequestHandled || outer.IsRejected || !admission.Demand.Cached ||
            authentication is null || authentication.IsRejected || authentication.IsRequestHandled || authentication.IsRequestSkipped ||
            principal is null || browser is null || !principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken) ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) != OpenIddictConstants.RequestTokenTypes.Private.CachedAuthorizationRequest ||
            !principal.GetPresenters().SequenceEqual([admission.Demand.ClientId], StringComparer.Ordinal) ||
            !ManagedLoginRules.IsSecret(principal.GetClaim(ManagedDemand.SelectorClaim)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await ProtocolStorageChild.RunAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
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
            await RequireInteractionAsync(scope, admission, registration, principal.GetClaim(ManagedDemand.SelectorClaim)!, browser,
                principal.GetTokenId()!, principal.GetExpirationDate()?.ToUnixTimeSeconds(), token);
            await transaction.CommitAsync(token);
            return true;
        });
        // T011 supplies managed-purpose password/MFA/consent. No native redemption or sign-in may run until that capability exists.
        throw ProtocolStorageOwner.Unavailable();
    }

    /// <summary>Rejects foreign, detached, expired, terminal or edited interactions without renewing the original deadline or consuming the native request.</summary>
    private static async ValueTask RequireInteractionAsync(ProtocolStorageScope scope, ManagedAdmission admission, ManagedRegistration registration,
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
    }
}
