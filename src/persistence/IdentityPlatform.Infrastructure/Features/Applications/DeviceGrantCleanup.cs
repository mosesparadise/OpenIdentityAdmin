using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Reclaims aged device grants under the governance lock after proving the original pair and every native descendant.</summary>
internal static class DeviceGrantCleanup
{
    private const int OriginalPairSize = 2;
    private static readonly string[] Types = [OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode, OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode,
        OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken];

    /// <summary>Examines a bounded batch after the retention floor. Active and stopped grant jobs keep their complete input.</summary>
    internal static async ValueTask RunAsync(ProtocolStorageScope scope, long now, TimeSpan skew, CancellationToken token)
    {
        var database = scope.Owner.Database;
        var cutoff = now - (long)TimeSpan.FromDays(ManagedLoginRules.RetentionDays).TotalSeconds;
        var windows = await scope.Owner.RunAsync(current => new ValueTask<DevicePollingWindow[]>(database.Set<DevicePollingWindow>()
            .Where(row => row.NativeAuthorizationId != null && row.ExpiresAt < cutoff && (row.LastCleanupAttempt == null || row.LastCleanupAttempt < now) &&
                !database.Set<ApplicationGrantJob>().Any(job => job.ApplicationId == row.ApplicationId && job.State != GrantJobState.Succeeded))
            .OrderBy(row => row.LastCleanupAttempt ?? 0).ThenBy(row => row.ExpiresAt).ThenBy(row => row.Digest).Take(ManagedLoginRules.CleanupLimit).ToArrayAsync(current)), token);
        foreach (var window in windows)
        {
            window.LastCleanupAttempt = now;
            var row = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction?>(database.Set<ManagedLoginInteraction>()
                .SingleOrDefaultAsync(human => human.AuthorizationId == window.NativeAuthorizationId, current)), token);
            if (row is not null && (row.DeviceEntryId is null || ManagedLoginRules.Digest(row.DeviceEntryId) != window.Digest ||
                row.ApplicationId != window.ApplicationId || row.RetainUntil >= now || row.ObservedAt > now))
            {
                continue;
            }
            if (!await RemoveGrantAsync(scope, window, row, now, skew, token))
            {
                continue;
            }
            await scope.Owner.RunAsync(async current =>
            {
                if (row is not null)
                {
                    await database.Set<ManagedScopeReference>().Where(value => value.SelectorDigest == row.SelectorDigest).ExecuteDeleteAsync(current);
                    database.Remove(row);
                }
                database.Remove(window);
                await database.SaveChangesAsync(current);
            }, token, write: true);
        }
        if (windows.Length != 0)
        {
            await scope.Owner.RunAsync(async current => { await database.SaveChangesAsync(current); }, token, write: true);
        }
    }

    /// <summary>Requires a known ad-hoc authorization, the exact device reference and only expired, matching token types before native cascade.</summary>
    private static async ValueTask<bool> RemoveGrantAsync(ProtocolStorageScope scope, DevicePollingWindow window, ManagedLoginInteraction? human, long now, TimeSpan skew, CancellationToken token)
    {
        var grants = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var grant = await grants.FindByIdAsync(window.NativeAuthorizationId!, token);
        if (grant is null)
        {
            return human is null && !await manager.FindByAuthorizationIdAsync(window.NativeAuthorizationId!, token).AnyAsync(token);
        }
        if (!await ProtocolStorageRows.ReloadAsync(scope, grant, token) || await grants.GetIdAsync(grant, token) != window.NativeAuthorizationId ||
            await grants.GetApplicationIdAsync(grant, token) != window.ApplicationId || !await grants.HasTypeAsync(grant, OpenIddictConstants.AuthorizationTypes.AdHoc, token) ||
            await grants.GetSubjectAsync(grant, token) != (human?.State == ManagedLoginState.DeviceApproved ? human.UserId : null))
        {
            return false;
        }
        var entries = new List<object>();
        await foreach (var entry in manager.FindByAuthorizationIdAsync(window.NativeAuthorizationId!, token))
        {
            if (entries.Count >= Types.Length)
            {
                return false;
            }
            entries.Add(entry);
        }
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (await manager.GetTypeAsync(entry, token) is not { } type || !Types.Contains(type, StringComparer.Ordinal) || !kinds.Add(type) ||
                await manager.GetIdAsync(entry, token) is not { } id || await manager.GetApplicationIdAsync(entry, token) != window.ApplicationId ||
                await manager.GetAuthorizationIdAsync(entry, token) != window.NativeAuthorizationId ||
                await manager.GetCreationDateAsync(entry, token) is not { } created || await manager.GetExpirationDateAsync(entry, token) is not { } expires ||
                ManagedLoginRules.RetainUntil(created.ToUnixTimeSeconds(), expires.ToUnixTimeSeconds(), skew) >= now ||
                (type == OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode && ManagedLoginRules.Digest(id) != window.Digest) ||
                (type == OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode && human is not null && id != human.RequestId))
            {
                return false;
            }
        }
        if (!kinds.Contains(OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode) || !kinds.Contains(OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode) ||
            (kinds.Count > OriginalPairSize && human?.State != ManagedLoginState.DeviceApproved))
        {
            return false;
        }
        foreach (var entry in entries)
        {
            await manager.DeleteAsync(entry, token);
        }
        await grants.DeleteAsync(grant, token);
        return true;
    }
}
