using System.Collections.Immutable;
using System.Security.Claims;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Coordinates device polling in a dedicated committed epoch before native validation may return a pending or authorized response.</summary>
internal static class DevicePollAdmission
{
    /// <summary>Creates a polling window only for the actual persisted native device entry and its initial registration and deadline.</summary>
    internal static async ValueTask RegisterLockedAsync(ProtocolStorageScope scope, OpenIddictRequest request, ClaimsPrincipal userCode, CancellationToken token)
    {
        await ManagedRegistration.LockAsync(scope, token);
        var registration = await DeviceClientRegistration.ReadLockedAsync(scope, request.ClientId ?? string.Empty, token) ?? throw ProtocolStorageOwner.Unavailable();
        var policy = registration.Policy;
        var scopes = request.GetScopes();
        var assigned = scopes.Where(name => name != OpenIddictConstants.Scopes.OpenId).ToArray();
        if (userCode.GetClaim(OpenIddictConstants.Claims.ClientId) != request.ClientId || userCode.GetPresenters() is not [var presenter] ||
            presenter != request.ClientId || !CatalogueRules.IsAssignment(assigned) || !scopes.ToHashSet(StringComparer.Ordinal).SetEquals(userCode.GetScopes()) ||
            assigned.Any(name => !registration.State.Permissions.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + name, StringComparer.Ordinal)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await ApplicationScopeSelection.ReadAsync(scope, assigned.ToImmutableArray(), token);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var deviceId = userCode.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId) ?? throw ProtocolStorageOwner.Unavailable();
        var authorizationId = await RequirePairAsync(scope, manager, policy.ApplicationId, userCode, token);
        var device = await manager.FindByIdAsync(deviceId, token);
        if (device is null || !await ProtocolStorageRows.ReloadAsync(scope, device, token) || await manager.GetIdAsync(device, token) != deviceId ||
            await manager.GetApplicationIdAsync(device, token) != policy.ApplicationId || await manager.GetAuthorizationIdAsync(device, token) != authorizationId ||
            await manager.GetSubjectAsync(device, token) is not null ||
            !await manager.HasTypeAsync(device, OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode, token) ||
            !await manager.HasStatusAsync(device, OpenIddictConstants.Statuses.Inactive, token) ||
            await manager.GetCreationDateAsync(device, token) is not { } created || await manager.GetExpirationDateAsync(device, token) is not { } expires ||
            expires.ToUnixTimeSeconds() - created.ToUnixTimeSeconds() != DevicePollingWindow.LifetimeSeconds)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var window = DevicePollingWindow.Start(deviceId, policy, created.ToUnixTimeSeconds());
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, window.CreatedAt);
        if (now >= window.ExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await StoreAsync(scope, window, now, token);
    }

    /// <summary>Checks the native user-code and anonymous authorization belong to the same registration and preserved scope set.</summary>
    private static async ValueTask<string> RequirePairAsync(ProtocolStorageScope scope, IOpenIddictTokenManager manager, string id,
        ClaimsPrincipal userCode, CancellationToken token)
    {
        var userId = userCode.GetTokenId() ?? throw ProtocolStorageOwner.Unavailable();
        var authId = userCode.GetAuthorizationId() ?? throw ProtocolStorageOwner.Unavailable();
        var user = await manager.FindByIdAsync(userId, token);
        if (user is null || !await ProtocolStorageRows.ReloadAsync(scope, user, token) || await manager.GetIdAsync(user, token) != userId ||
            await manager.GetApplicationIdAsync(user, token) != id || await manager.GetAuthorizationIdAsync(user, token) != authId ||
            await manager.GetSubjectAsync(user, token) is not null || !await manager.HasTypeAsync(user, OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode, token) ||
            !await manager.HasStatusAsync(user, OpenIddictConstants.Statuses.Valid, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.FindByIdAsync(authId, token);
        if (authorization is null || !await ProtocolStorageRows.ReloadAsync(scope, authorization, token) ||
            await authorizations.GetIdAsync(authorization, token) != authId || await authorizations.GetApplicationIdAsync(authorization, token) != id ||
            !await authorizations.HasTypeAsync(authorization, OpenIddictConstants.AuthorizationTypes.AdHoc, token) ||
            !await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, token) ||
            await authorizations.GetSubjectAsync(authorization, token) is not null ||
            !userCode.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals(await authorizations.GetScopesAsync(authorization, token)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return authId;
    }

    /// <summary>Reclaims a bounded batch and inserts the window under the same original transaction as its native pair.</summary>
    private static ValueTask StoreAsync(ProtocolStorageScope scope, DevicePollingWindow window, long now, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var windows = scope.Owner.Database.Set<DevicePollingWindow>();
            var expired = await windows.Where(row => row.ExpiresAt < now).OrderBy(row => row.ExpiresAt).ThenBy(row => row.Digest)
                .Select(row => row.Digest).Take(ManagedLoginRules.CleanupLimit).ToArrayAsync(current);
            if (expired.Length != 0)
            {
                await windows.Where(row => expired.Contains(row.Digest) && row.ExpiresAt < now).ExecuteDeleteAsync(current);
            }
            if (await windows.LongCountAsync(current) >= ManagedLoginRules.RetainedLimit)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            windows.Add(window);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);

    /// <summary>Commits each cadence observation under current native registration authority before handing an admitted poll back to OpenIddict.</summary>
    internal static async ValueTask<DevicePollResult> PollAsync(ProtocolStorageScope scope, string clientId, string externalCode, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await ManagedRegistration.LockAsync(scope, token);
        var current = await DeviceClientRegistration.ReadLockedAsync(scope, clientId, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var tokenRow = current is null ? null : await manager.FindByReferenceIdAsync(externalCode, token);
        var result = DevicePollResult.Unavailable;
        if (tokenRow is not null && await ProtocolStorageRows.ReloadAsync(scope, tokenRow, token) &&
            await manager.HasTypeAsync(tokenRow, OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode, token) &&
            await manager.GetApplicationIdAsync(tokenRow, token) == current!.Value.Policy.ApplicationId &&
            await manager.GetIdAsync(tokenRow, token) is { } id)
        {
            result = await AdvanceAsync(scope, current.Value.Policy, ManagedLoginRules.Digest(id), token);
        }
        await transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Reads and writes the same cadence row within governance serialization and the request's storage deadline.</summary>
    private static ValueTask<DevicePollResult> AdvanceAsync(ProtocolStorageScope scope, ApplicationPolicy policy, string digest, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var window = await scope.Owner.Database.Set<DevicePollingWindow>().SingleOrDefaultAsync(row => row.Digest == digest, current);
            if (window is null)
            {
                return DevicePollResult.Unavailable;
            }
            var result = window.Poll(policy, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());
            if (result != DevicePollResult.Unavailable)
            {
                await scope.Owner.Database.SaveChangesAsync(current);
            }
            return result;
        }, token, write: true);
}
