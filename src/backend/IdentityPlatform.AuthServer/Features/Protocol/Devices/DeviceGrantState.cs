using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Devices;

/// <summary>Checks native token and authorization rows independently of the generated response and the tracked human ceremony.</summary>
internal static class DeviceGrantState
{
    /// <summary>Checks both native entries and updates the original anonymous grant with the subject of the successful managed approval.</summary>
    internal static async ValueTask DecisionAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, bool accepted, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        await RequireTokenAsync(scope, manager, row.RequestId, row, OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode,
            accepted ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Rejected, null, token);
        var device = await RequireTokenAsync(scope, manager, row.DeviceEntryId, row, OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode,
            accepted ? OpenIddictConstants.Statuses.Valid : OpenIddictConstants.Statuses.Rejected, accepted ? row.UserId : null, token);
        if ((await manager.GetExpirationDateAsync(device, token))?.ToUnixTimeSeconds() != row.NativeDependencyExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (accepted)
        {
            var auths = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
            var auth = await auths.FindByIdAsync(row.AuthorizationId!, token) ?? throw ProtocolStorageOwner.Unavailable();
            if (!await ProtocolStorageRows.ReloadAsync(scope, auth, token) || await auths.GetSubjectAsync(auth, token) is not null ||
                await auths.GetApplicationIdAsync(auth, token) != row.ApplicationId || !await auths.HasStatusAsync(auth, OpenIddictConstants.Statuses.Valid, token))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            var description = new OpenIddictAuthorizationDescriptor();
            await auths.PopulateAsync(description, auth, token);
            description.Subject = row.UserId;
            await auths.UpdateAsync(auth, description, token);
            await RequireAuthorizationAsync(scope, row, token);
        }
    }

    /// <summary>Requires that native redemption marked only the original device and persisted the expected access and identity tokens.</summary>
    internal static async ValueTask IssuanceAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, OpenIddictServerEvents.ProcessSignInContext issued, CancellationToken token)
    {
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        await RequireTokenAsync(scope, tokens, row.DeviceEntryId, row, OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode, OpenIddictConstants.Statuses.Redeemed, row.UserId, token);
        await RequireTokenAsync(scope, tokens, issued.AccessTokenPrincipal?.GetTokenId(), row, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.Statuses.Valid, row.UserId, token);
        await RequireTokenAsync(scope, tokens, issued.IdentityTokenPrincipal?.GetTokenId(), row, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken, OpenIddictConstants.Statuses.Valid, row.UserId, token);
        await RequireAuthorizationAsync(scope, row, token);
    }

    /// <summary>Reads the exact persisted, subject-bound native authorization and its approved scope set.</summary>
    private static async ValueTask RequireAuthorizationAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await manager.FindByIdAsync(row.AuthorizationId!, token);
        if (authorization is null || !await ProtocolStorageRows.ReloadAsync(scope, authorization, token) || await manager.GetIdAsync(authorization, token) != row.AuthorizationId ||
            await manager.GetApplicationIdAsync(authorization, token) != row.ApplicationId || await manager.GetSubjectAsync(authorization, token) != row.UserId ||
            !await manager.HasTypeAsync(authorization, OpenIddictConstants.AuthorizationTypes.AdHoc, token) ||
            !await manager.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, token) ||
            !ManagedScopeBinding.Read(row).Granted.ToHashSet(StringComparer.Ordinal).SetEquals(await manager.GetScopesAsync(authorization, token)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Rereads a native entry and checks its exact stable identity, registration, original authorization, status, type and subject.</summary>
    private static async ValueTask<object> RequireTokenAsync(ProtocolStorageScope scope, IOpenIddictTokenManager manager, string? id,
        ManagedLoginInteraction row, string type, string status, string? subject, CancellationToken token)
    {
        var entry = id is null ? null : await manager.FindByIdAsync(id, token);
        if (entry is null || !await ProtocolStorageRows.ReloadAsync(scope, entry, token) || await manager.GetIdAsync(entry, token) != id ||
            await manager.GetApplicationIdAsync(entry, token) != row.ApplicationId || await manager.GetAuthorizationIdAsync(entry, token) != row.AuthorizationId ||
            await manager.GetSubjectAsync(entry, token) != subject || !await manager.HasTypeAsync(entry, type, token) || !await manager.HasStatusAsync(entry, status, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return entry;
    }
}
