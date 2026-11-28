using System.Collections.Frozen;
using System.Text.Json.Nodes;
using IdentityPlatform.Admin.Api.Features.Documentation;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Reads current account/session and persisted permission under rollback-only ownership; no factor keys or clock writes are used.</summary>
internal static class ApiCurrentAccess
{
    private const string ReadPermission = "applications.read";
    private const string ScopePermission = "scopes.read";
    private const string AudiencePermission = "audiences.read";

    /// <summary>Distinguishes current authentication denial from an otherwise eligible account lacking its persisted grant.</summary>
    internal static async Task<(int Status, long SourceExpiry)> ReadAsync(IServiceProvider services, PlatformDbContext database,
        CeremonyTransaction transaction, ApiIdentity identity, string callback, TimeProvider clock, ApiReadResource resource = ApiReadResource.Applications)
    {
        var current = await PermissionsAsync(services, database, transaction, identity, callback, clock);
        return current is null ? (StatusCodes.Status401Unauthorized, 0) :
            (current.Grants.Contains(Permission(resource)) ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, current.ExpiresAt);
    }

    /// <summary>Derives the complete current grant set from the same independently verified source session and native client profile as management reads.</summary>
    private static async Task<GrantObservation?> PermissionsAsync(IServiceProvider services, PlatformDbContext database,
        CeremonyTransaction transaction, ApiIdentity identity, string callback, TimeProvider clock)
    {
        transaction.RequireApiRead(database, identity.Subject);
        var state = await ApiSourceObservation.ReadAsync(database, identity, clock, transaction.Token);
        if (state is null || !await ApiReadProfile.AllowsAsync(services, database, transaction, identity, callback, clock.GetUtcNow().ToUnixTimeSeconds()))
        {
            return null;
        }
        var grants = await new GovernanceGrants(database).ReadApiAsync(transaction, identity.Subject);
        transaction.RequireApiRead(database, identity.Subject);
        return new(grants.Permissions(identity.Subject), state.ExpiresAt);
    }

    /// <summary>Projects the native contract within the live reader transaction and the earliest current token or source deadline.</summary>
    internal static async Task<JsonObject?> ProjectAsync(IServiceProvider services, PlatformDbContext database, CeremonyTransaction transaction,
        ApiIdentity identity, string callback, TimeProvider clock, JsonObject document)
    {
        transaction.RequireApiRead(database, identity.Subject);
        var started = clock.GetUtcNow();
        var current = await PermissionsAsync(services, database, transaction, identity, callback, clock);
        if (current is null)
        {
            return null;
        }
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await tokens.FindByIdAsync(identity.TokenId, transaction.Token);
        var persisted = entry is null ? null : await tokens.GetExpirationDateAsync(entry, transaction.Token);
        if (persisted is null)
        {
            return null;
        }
        var deadline = Math.Min(current.ExpiresAt, Math.Min(identity.ExpiresAt.ToUnixTimeSeconds(), persisted.Value.ToUnixTimeSeconds()));
        if (started.ToUnixTimeSeconds() >= deadline)
        {
            return null;
        }
        var filtered = PermissionContract.Create(document, current.Grants);
        var finished = clock.GetUtcNow();
        transaction.RequireApiRead(database, identity.Subject);
        return finished >= started && finished.ToUnixTimeSeconds() < deadline ? filtered : null;
    }

    private sealed record GrantObservation(FrozenSet<string> Grants, long ExpiresAt);

    /// <summary>Maps only fixed read surfaces; an unknown resource cannot fall through to another feature's grant.</summary>
    private static string Permission(ApiReadResource resource) => resource switch
    {
        ApiReadResource.Applications => ReadPermission,
        ApiReadResource.Scopes => ScopePermission,
        ApiReadResource.Audiences => AudiencePermission,
        _ => throw new InvalidOperationException()
    };

}

/// <summary>Enumerates fixed management read authority without accepting route-selected permission strings.</summary>
internal enum ApiReadResource
{
    Applications, Scopes, Audiences
}
