using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Verifies the current fixed B3 registration using public metadata only, under its declared profile lock.</summary>
internal static class ApiReadProfile
{
    /// <summary>Requires exact persisted native identifiers and the complete enabled registration, never client-ID equality alone.</summary>
    internal static async Task<bool> AllowsAsync(IServiceProvider services, PlatformDbContext database,
        CeremonyTransaction transaction, ApiIdentity identity, string callback, long now)
    {
        transaction.RequireApiRead(database, identity.Subject);
        var token = transaction.Token;
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == AdministratorProtocolProfile.SingletonId, token);
        var result = await MatchesAsync(services, profile, identity, callback, now, token);
        transaction.RequireApiRead(database, identity.Subject);
        return result;
    }

    /// <summary>Uses current owned database observations and explicit row reloads without nesting native manager execution leases.</summary>
    internal static async Task<DateTimeOffset?> ReadOwnedAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        ApiIdentity identity, string callback, CancellationToken token)
    {
        var database = (PlatformDbContext)scope.Owner.Database;
        ownership.RequireCurrent(database, token);
        var profile = await ownership.RunAsync(database, async current => await database.Set<AdministratorProtocolProfile>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == AdministratorProtocolProfile.SingletonId, current), token);
        if (!await MatchesAsync(scope.Services, profile, identity, callback, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds(), token, scope))
        {
            return null;
        }
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await tokens.FindByIdAsync(identity.TokenId, token);
        var expiry = entry is null ? null : await tokens.GetExpirationDateAsync(entry, token);
        ownership.RequireCurrent(database, token);
        return expiry is { } value && value > scope.Owner.Clock.GetUtcNow() ? value : null;
    }

    /// <summary>Compares actual native configuration and lifecycle bindings; owned calls reload tracked rows before inspecting them.</summary>
    private static async Task<bool> MatchesAsync(IServiceProvider services, AdministratorProtocolProfile? profile,
        ApiIdentity identity, string callback, long now, CancellationToken token, ProtocolStorageScope? owned = null)
    {
        if (profile is not { Enabled: true, Revision: > 0, ObservedAt: >= 0 } || profile.ObservedAt > now || profile.RedirectUri != callback)
        {
            return false;
        }
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = services.GetRequiredService<IOpenIddictScopeManager>();
        var application = await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token);
        var scope = await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, token);
        if (application is null || scope is null || !await RefreshAsync(owned, application, token) || !await RefreshAsync(owned, scope, token) || await applications.GetIdAsync(application, token) != profile.ApplicationId ||
            await scopes.GetIdAsync(scope, token) != profile.ScopeId)
        {
            return false;
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        var resource = new OpenIddictScopeDescriptor();
        await applications.PopulateAsync(descriptor, application, token);
        await scopes.PopulateAsync(resource, scope, token);
        var matches = Matches(descriptor, resource, profile, callback);
        var bindings = await NativeBindingsMatchAsync(services, identity, profile.ApplicationId, owned, token);
        return matches && bindings;
    }

    /// <summary>Requires native lifecycle rows to belong to the exact signed subject and fixed registered client, independent of token role claims.</summary>
    private static async Task<bool> NativeBindingsMatchAsync(IServiceProvider services, ApiIdentity identity, string applicationId, ProtocolStorageScope? owned, CancellationToken token)
    {
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var entry = await tokens.FindByIdAsync(identity.TokenId, token);
        var authorization = await authorizations.FindByIdAsync(identity.AuthorizationId, token);
        return entry is not null && authorization is not null &&
            await RefreshAsync(owned, entry, token) && await RefreshAsync(owned, authorization, token) &&
            await tokens.GetIdAsync(entry, token) == identity.TokenId &&
            await tokens.GetApplicationIdAsync(entry, token) == applicationId &&
            await tokens.GetSubjectAsync(entry, token) == identity.Subject &&
            await tokens.GetAuthorizationIdAsync(entry, token) == identity.AuthorizationId &&
            await tokens.HasTypeAsync(entry, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, token) &&
            await tokens.HasStatusAsync(entry, OpenIddictConstants.Statuses.Valid, token) &&
            await authorizations.GetIdAsync(authorization, token) == identity.AuthorizationId &&
            await authorizations.GetApplicationIdAsync(authorization, token) == applicationId &&
            await authorizations.GetSubjectAsync(authorization, token) == identity.Subject &&
            await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Leaves existing rollback-only reads unchanged while requiring fresh persisted rows in owned rechecks.</summary>
    private static async ValueTask<bool> RefreshAsync(ProtocolStorageScope? owned, object row, CancellationToken token) =>
        owned is null || await ProtocolStorageRows.ReloadAsync(owned, row, token);

    /// <summary>Derives the expected public manifest while checking the original native key for private or extra fields.</summary>
    private static bool Matches(OpenIddictApplicationDescriptor application, OpenIddictScopeDescriptor scope,
        AdministratorProtocolProfile profile, string callback)
    {
        if (application.JsonWebKeySet is not { Keys.Count: 1, AdditionalData.Count: 0 } keys)
        {
            return false;
        }
        var native = keys.Keys[0];
        var raw = JsonSerializer.Serialize(new
        {
            kty = native.Kty,
            kid = native.Kid,
            alg = native.Alg,
            use = native.Use,
            n = native.N,
            e = native.E
        });
        var key = AdministratorPublicKey.Parse(raw);
        return key is not null && key.Matches(native) && key.Fingerprint == profile.PublicKeyFingerprint &&
            AdministratorRegistrationPolicy.Create(callback, key)?.ForBrowserSessions() is { } expected && expected.Matches(application, scope);
    }
}
