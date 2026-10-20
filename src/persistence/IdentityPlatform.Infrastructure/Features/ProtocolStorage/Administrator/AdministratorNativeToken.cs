using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Checks fresh native row bindings under the caller's owning-store lock before native status validation or redemption.</summary>
internal static class AdministratorNativeToken
{
    /// <summary>Requires a structurally bound native server artifact whose earlier persisted/protected deadline is still strictly current.</summary>
    internal static async ValueTask<DateTimeOffset?> ExpirationAsync(ProtocolStorageScope scope, ClaimsPrincipal principal,
        string expectedType, string? applicationId, CancellationToken cancellation)
    {
        var bound = await ReadBoundAsync(scope, principal, expectedType, applicationId, cancellation);
        return IsCurrent(bound, scope.Owner.Clock) ? bound : null;
    }

    /// <summary>Returns only a matching client-state bound, including expiry, so native redemption can commit before strict acceptance denial.</summary>
    internal static ValueTask<DateTimeOffset?> StateExpirationAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, CancellationToken cancellation) =>
        ReadBoundAsync(scope, principal, OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, applicationId: null, cancellation);

    /// <summary>Requires fresh native type/application/authorization/subject bindings and both finite deadlines without reconstructing token cryptography.</summary>
    private static async ValueTask<DateTimeOffset?> ReadBoundAsync(ProtocolStorageScope scope, ClaimsPrincipal principal,
        string expectedType, string? applicationId, CancellationToken cancellation)
    {
        var identifier = principal.GetTokenId();
        if (string.IsNullOrEmpty(identifier) || !principal.HasTokenType(expectedType))
        {
            return null;
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(identifier, cancellation);
        var subject = expectedType == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken ? null : principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (entry is null || !await ProtocolStorageRows.ReloadAsync(scope, entry, cancellation) || !string.Equals(await manager.GetTypeAsync(entry, cancellation), expectedType, StringComparison.Ordinal) ||
            !string.Equals(await manager.GetApplicationIdAsync(entry, cancellation), applicationId, StringComparison.Ordinal) ||
            !string.Equals(await manager.GetAuthorizationIdAsync(entry, cancellation), principal.GetAuthorizationId(), StringComparison.Ordinal) ||
            !string.Equals(await manager.GetSubjectAsync(entry, cancellation), subject, StringComparison.Ordinal))
        {
            return null;
        }
        var expiration = await manager.GetExpirationDateAsync(entry, cancellation);
        var principalExpiration = principal.GetExpirationDate();
        if (expiration is not { } expires || principalExpiration is not { } principalExpires)
        {
            return null;
        }
        return expires < principalExpires ? expires : principalExpires;
    }

    /// <summary>Rechecks expiry after native awaited work; equality never extends a consumed proof's useful lifetime.</summary>
    internal static bool IsCurrent(DateTimeOffset? expiration, TimeProvider clock) =>
        expiration is { } bound && bound > clock.GetUtcNow();
}
