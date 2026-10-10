using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Policy;

public sealed class PilotEligibility(PlatformDbContext database, UserManager<PlatformUser> users,
    IOpenIddictApplicationManager applications, IOpenIddictScopeManager scopes, TimeProvider time)
{
    public async Task<bool> UserAsync(PlatformUser user, CancellationToken cancellationToken = default)
    {
        return user.IsEnabled && user.LockoutEnabled && !user.TwoFactorEnabled && !await users.IsLockedOutAsync(user) &&
            (await users.GetRolesAsync(user)).Count == 0 &&
            await database.Set<PilotManifest>().AnyAsync(value => value.Id == PilotProfile.Id && value.UserId == user.Id, cancellationToken);
    }

    public async Task<LocalIdentitySession?> SessionAsync(string? sessionId, string? userId, CancellationToken cancellationToken = default)
    {
        if (sessionId is null || userId is null)
        {
            return null;
        }
        var session = await database.Set<LocalIdentitySession>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId, cancellationToken);
        if (session is null || session.Revoked || session.ExpiresAt <= time.GetUtcNow().ToUnixTimeSeconds())
        {
            return null;
        }
        var user = await users.FindByIdAsync(userId);
        return user is not null && await UserAsync(user, cancellationToken) &&
            string.Equals(session.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal) ? session : null;
    }

    public async Task<bool> RegistrationAsync(string? clientId, IEnumerable<string> grantedScopes, CancellationToken cancellationToken)
    {
        if (!string.Equals(clientId, PilotProfile.ClientId, StringComparison.Ordinal))
        {
            return false;
        }
        var requested = grantedScopes.ToHashSet(StringComparer.Ordinal);
        if (!requested.SetEquals([OpenIddictConstants.Scopes.OpenId, PilotProfile.Scope]))
        {
            return false;
        }
        var manifest = await database.Set<PilotManifest>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == PilotProfile.Id, cancellationToken);
        var application = await applications.FindByClientIdAsync(PilotProfile.ClientId, cancellationToken);
        var scope = await scopes.FindByNameAsync(PilotProfile.Scope, cancellationToken);
        return manifest is not null && application is not null && scope is not null &&
            string.Equals(await applications.GetIdAsync(application, cancellationToken), manifest.ApplicationId, StringComparison.Ordinal) &&
            string.Equals(await scopes.GetIdAsync(scope, cancellationToken), manifest.ScopeId, StringComparison.Ordinal) &&
            await applications.HasApplicationTypeAsync(application, OpenIddictConstants.ApplicationTypes.Web, cancellationToken) &&
            (await applications.GetSettingsAsync(application, cancellationToken)).Count == 0 &&
            await applications.HasClientTypeAsync(application, OpenIddictConstants.ClientTypes.Confidential, cancellationToken) &&
            await applications.HasConsentTypeAsync(application, OpenIddictConstants.ConsentTypes.Implicit, cancellationToken) &&
            (await applications.GetRedirectUrisAsync(application, cancellationToken)).SequenceEqual([manifest.RedirectUri], StringComparer.Ordinal) &&
            PilotProfile.Permissions.SetEquals(await applications.GetPermissionsAsync(application, cancellationToken)) &&
            PilotProfile.Requirements.SetEquals(await applications.GetRequirementsAsync(application, cancellationToken)) &&
            Enabled(await applications.GetPropertiesAsync(application, cancellationToken)) &&
            Enabled(await scopes.GetPropertiesAsync(scope, cancellationToken)) &&
            (await scopes.GetResourcesAsync(scope, cancellationToken)).SequenceEqual([PilotProfile.Resource], StringComparer.Ordinal);
    }

    public static bool Enabled(IReadOnlyDictionary<string, System.Text.Json.JsonElement> properties) =>
        properties.TryGetValue("profile", out var profile) && profile.ValueKind == System.Text.Json.JsonValueKind.String &&
        profile.GetString() == PilotProfile.Id && properties.TryGetValue("enabled", out var enabled) &&
        enabled.ValueKind == System.Text.Json.JsonValueKind.True;

    public static ClaimsPrincipal Principal(LocalIdentitySession session)
    {
        var identity = new ClaimsIdentity(OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, session.UserId)
            .SetClaim(OpenIddictConstants.Claims.AuthenticationTime, session.AuthenticatedAt)
            .SetClaim(PilotProfile.SessionClaim, session.Id);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId, PilotProfile.Scope);
        principal.SetResources(PilotProfile.Resource);
        principal.SetDestinations(claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.AuthenticationTime => [OpenIddictConstants.Destinations.IdentityToken],
            _ => []
        });
        return principal;
    }
}
