using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Checks fresh native row bindings under the caller's owning-store lock before native status validation or redemption.</summary>
internal static class AdministratorNativeToken
{
    /// <summary>Requires a real matching row and strict native/principal expiry without reconstructing or revalidating token cryptography.</summary>
    internal static async ValueTask<DateTimeOffset?> ExpirationAsync(ProtocolStorageScope scope, ClaimsPrincipal principal,
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
        var bound = expires < principalExpires ? expires : principalExpires;
        return bound > scope.Owner.Clock.GetUtcNow() ? bound : null;
    }

    /// <summary>Rechecks expiry after native awaited work; equality never extends a consumed proof's useful lifetime.</summary>
    internal static bool IsCurrent(DateTimeOffset? expiration, TimeProvider clock) =>
        expiration is { } bound && bound > clock.GetUtcNow();
}
