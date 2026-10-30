using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Expires unfinished interactions and removes only retained terminal request-only dependencies in finite batches.</summary>
internal static class ManagedLoginCleanup
{
    /// <summary>Never reopens terminal proof or deletes a live/consumed code lineage; later issuance owns its separate cleanup dependency.</summary>
    internal static async ValueTask RunAsync(ProtocolStorageScope scope, long now, TimeSpan skew, CancellationToken token)
    {
        await scope.Owner.RunAsync(async current =>
        {
            var rows = await scope.Owner.Database.Set<ManagedLoginInteraction>()
                .Where(row => row.State <= ManagedLoginState.Approved && row.ExpiresAt <= now && row.ObservedAt <= now)
                .OrderBy(row => row.ExpiresAt).ThenBy(row => row.SelectorDigest).Take(ManagedLoginRules.CleanupLimit).ToArrayAsync(current);
            foreach (var row in rows)
            {
                row.State = ManagedLoginState.Expired;
                row.Revision = checked(row.Revision + 1);
                row.ObservedAt = now;
            }
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        var retained = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction[]>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .Where(row => (row.State == ManagedLoginState.Expired || row.State == ManagedLoginState.Denied) && row.RetainUntil < now && row.ObservedAt <= now &&
                row.AuthorizationId == null && row.CodeId == null).OrderBy(row => row.RetainUntil).ThenBy(row => row.SelectorDigest)
            .Take(ManagedLoginRules.CleanupLimit).ToArrayAsync(current)), token);
        foreach (var row in retained)
        {
            if (row.RetainUntil < ManagedLoginRules.RetainUntil(row.CreatedAt, row.NativeDependencyExpiresAt ?? row.ExpiresAt, skew) ||
                !await RemoveRequestAsync(scope, row, now, skew, token))
            {
                continue;
            }
            await scope.Owner.RunAsync(async current =>
            {
                scope.Owner.Database.Remove(row);
                await scope.Owner.Database.SaveChangesAsync(current);
            }, token, write: true);
        }
    }

    /// <summary>Uses native managers only after proving the exact anonymous request row is beyond both age and expiry retention.</summary>
    private static async ValueTask<bool> RemoveRequestAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, long now,
        TimeSpan skew, CancellationToken token)
    {
        if (row.RequestId is null)
        {
            return true;
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(row.RequestId, token);
        if (entry is null)
        {
            return true;
        }
        var created = await manager.GetCreationDateAsync(entry, token);
        var expiry = await manager.GetExpirationDateAsync(entry, token);
        if (await manager.GetIdAsync(entry, token) != row.RequestId || await manager.GetApplicationIdAsync(entry, token) != row.ApplicationId ||
            await manager.GetTypeAsync(entry, token) != OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken ||
            await manager.GetAuthorizationIdAsync(entry, token) is not null || await manager.GetSubjectAsync(entry, token) is not null ||
            created is null || expiry is null || ManagedLoginRules.RetainUntil(created.Value.ToUnixTimeSeconds(), expiry.Value.ToUnixTimeSeconds(), skew) >= now)
        {
            return false;
        }
        await manager.DeleteAsync(entry, token);
        return true;
    }
}
