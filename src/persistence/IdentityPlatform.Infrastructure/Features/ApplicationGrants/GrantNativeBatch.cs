using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Revokes independent bounded native families inside the caller's serialized owned transaction.</summary>
internal static class GrantNativeBatch
{
    internal const int FamilyLimit = 8;

    /// <summary>Materializes scalar IDs before manager calls so no database reader or execution lease remains active during native writes.</summary>
    internal static async Task<GrantBatchCount> RevokeAsync(ProtocolStorageScope scope, string applicationId, CancellationToken token)
    {
        var authorizations = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .AsNoTracking().Where(row => row.Application != null && row.Application.Id == applicationId && row.Status == OpenIddictConstants.Statuses.Valid)
            .OrderBy(row => row.Id).Select(row => row.Id!).Take(FamilyLimit).ToArrayAsync(current), token);
        var tokens = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().Where(row => row.Application != null && row.Application.Id == applicationId &&
                (row.Status == OpenIddictConstants.Statuses.Valid || row.Status == OpenIddictConstants.Statuses.Inactive))
            .OrderBy(row => row.Id).Select(row => row.Id!).Take(FamilyLimit).ToArrayAsync(current), token);
        foreach (var id in authorizations)
        {
            await AuthorizationAsync(scope, applicationId, id, token);
        }
        foreach (var id in tokens)
        {
            await TokenAsync(scope, applicationId, id, token);
        }
        return new(authorizations, tokens);
    }

    /// <summary>Rechecks every changed native entry after all audit/save callbacks, so later callbacks cannot conceal an earlier reverted status.</summary>
    internal static async Task VerifyAsync(ProtocolStorageScope scope, string applicationId, GrantBatchCount batch, CancellationToken token)
    {
        if (batch.Authorizations == 0 && batch.Tokens == 0 && await RemainingAsync(scope, applicationId, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        foreach (var id in batch.AuthorizationIds)
        {
            var row = await authorizations.FindByIdAsync(id, token);
            if (row is null || !await ProtocolStorageRows.ReloadAsync(scope, row, token) || await authorizations.GetIdAsync(row, token) != id ||
                await authorizations.GetApplicationIdAsync(row, token) != applicationId || await authorizations.GetStatusAsync(row, token) != OpenIddictConstants.Statuses.Revoked)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
        foreach (var id in batch.TokenIds)
        {
            var row = await tokens.FindByIdAsync(id, token);
            if (row is null || !await ProtocolStorageRows.ReloadAsync(scope, row, token) || await tokens.GetIdAsync(row, token) != id ||
                await tokens.GetApplicationIdAsync(row, token) != applicationId || await tokens.GetStatusAsync(row, token) != OpenIddictConstants.Statuses.Revoked)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
    }

    /// <summary>Reconciles completion after save callbacks without an unbounded count or scan materialization.</summary>
    private static ValueTask<bool> RemainingAsync(ProtocolStorageScope scope, string applicationId, CancellationToken token) => scope.Owner.RunAsync(async current =>
        await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().AnyAsync(row =>
            row.Application != null && row.Application.Id == applicationId && row.Status == OpenIddictConstants.Statuses.Valid, current) ||
        await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().AnyAsync(row => row.Application != null && row.Application.Id == applicationId &&
            (row.Status == OpenIddictConstants.Statuses.Valid || row.Status == OpenIddictConstants.Statuses.Inactive), current), token);

    /// <summary>Requires a fresh exact eligible native authorization, manager success and independently reloaded revoked status.</summary>
    private static async Task AuthorizationAsync(ProtocolStorageScope scope, string applicationId, string id, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var row = await manager.FindByIdAsync(id, token);
        if (row is null || !await ProtocolStorageRows.ReloadAsync(scope, row, token) ||
            await manager.GetIdAsync(row, token) != id || await manager.GetApplicationIdAsync(row, token) != applicationId ||
            await manager.GetStatusAsync(row, token) != OpenIddictConstants.Statuses.Valid || !await manager.TryRevokeAsync(row, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (!await ProtocolStorageRows.ReloadAsync(scope, row, token) || await manager.GetIdAsync(row, token) != id ||
            await manager.GetApplicationIdAsync(row, token) != applicationId || await manager.GetStatusAsync(row, token) != OpenIddictConstants.Statuses.Revoked)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Includes authorization-less native tokens and never changes redeemed, rejected or already revoked entries.</summary>
    private static async Task TokenAsync(ProtocolStorageScope scope, string applicationId, string id, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(id, token);
        if (row is null || !await ProtocolStorageRows.ReloadAsync(scope, row, token) ||
            await manager.GetIdAsync(row, token) != id || await manager.GetApplicationIdAsync(row, token) != applicationId ||
            await manager.GetStatusAsync(row, token) is not (OpenIddictConstants.Statuses.Valid or OpenIddictConstants.Statuses.Inactive) ||
            !await manager.TryRevokeAsync(row, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (!await ProtocolStorageRows.ReloadAsync(scope, row, token) || await manager.GetIdAsync(row, token) != id ||
            await manager.GetApplicationIdAsync(row, token) != applicationId || await manager.GetStatusAsync(row, token) != OpenIddictConstants.Statuses.Revoked)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}

/// <summary>Reports only successful native transitions in the caller's still-uncommitted batch.</summary>
internal sealed record GrantBatchCount(IReadOnlyList<string> AuthorizationIds, IReadOnlyList<string> TokenIds)
{
    internal int Authorizations => AuthorizationIds.Count;
    internal int Tokens => TokenIds.Count;
}
