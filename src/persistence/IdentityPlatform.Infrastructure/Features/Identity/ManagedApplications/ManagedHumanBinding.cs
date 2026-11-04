using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Rechecks actual native request/application bindings under acquired governance before every human phase.</summary>
internal static class ManagedHumanBinding
{
    /// <summary>Loads only an exact browser, phase and original-request match; a protected form alone cannot authorize a transition.</summary>
    internal static ValueTask<ManagedLoginInteraction> RowAsync(ProtocolStorageScope scope, ManagedHumanRequest request, CancellationToken token) =>
        scope.Owner.RunAsync<ManagedLoginInteraction>(async current =>
        {
            if (!ManagedLoginRules.IsSecret(request.Selector) || !ManagedLoginRules.IsSecret(request.BrowserDigest) ||
                !CeremonySecrets.IsIdentifier(request.Correlation))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            var digest = ManagedLoginRules.Digest(request.Selector);
            var row = await scope.Owner.Database.Set<ManagedLoginInteraction>().SingleOrDefaultAsync(value => value.SelectorDigest == digest, current);
            if (row is null || row.SelectorDigest != digest || row.BrowserDigest != request.BrowserDigest || row.State != request.State ||
                row.Revision != request.Revision || row.DemandDigest != request.DemandDigest || row.RequestId != request.RequestId ||
                row.RequestExpiresAt != request.ExpiresAt || row.ExpiresAt != request.ExpiresAt || row.State == ManagedLoginState.Consumed ||
                row.State == ManagedLoginState.Expired || ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt) >= row.ExpiresAt)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return row;
        }, token);

    /// <summary>Uses native managers to reject revoked/detached requests and native registration drift without selecting alternate stores.</summary>
    internal static async ValueTask<ManagedRegistration> NativeAsync(ProtocolStorageScope scope, ManagedHumanRequest request, ManagedLoginInteraction row, CancellationToken token)
    {
        var registration = await ManagedRegistration.ReadAsync(scope, request.ClientId, token);
        if (registration is null || registration.ApplicationId != row.ApplicationId || registration.Revision != row.ApplicationRevision ||
            !registration.Redirects.Contains(request.Callback, StringComparer.Ordinal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(request.RequestId, token);
        if (entry is null || !await ProtocolStorageRows.ReloadAsync(scope, entry, token) || await manager.GetIdAsync(entry, token) != row.RequestId ||
            await manager.GetApplicationIdAsync(entry, token) != row.ApplicationId || await manager.GetAuthorizationIdAsync(entry, token) is not null ||
            await manager.GetSubjectAsync(entry, token) is not null || await manager.GetStatusAsync(entry, token) != OpenIddictConstants.Statuses.Valid ||
            await manager.GetTypeAsync(entry, token) != OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken ||
            (await manager.GetExpirationDateAsync(entry, token))?.ToUnixTimeSeconds() != row.RequestExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return registration;
    }
}
