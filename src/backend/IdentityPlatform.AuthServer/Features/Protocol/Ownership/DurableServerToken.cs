using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Commits native token validation side effects and early redemption in a fresh serialized child before issuance.</summary>
internal static class DurableServerToken
{
    /// <summary>Preserves native rejection details and durable replay-chain revocation without continuing dispatch in a closed owner.</summary>
    internal static async ValueTask RunAsync(IServiceScopeFactory factory, OpenIddictServerEvents.ValidateTokenContext validation,
        Func<IServiceProvider, ValueTask>? consume = null)
    {
        var clock = ProtocolStorageAmbient.Current?.Request.Clock ?? throw ProtocolStorageOwner.Unavailable();
        var expiration = await ProtocolStorageChild.RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var bound = await ExpirationAsync(scope, validation, cancellation);
            if (bound is null)
            {
                validation.Reject(OpenIddictConstants.Errors.InvalidToken);
            }
            else
            {
                await scope.Services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateTokenEntry>().HandleAsync(validation);
                if (!validation.IsRejected && !string.IsNullOrEmpty(validation.AuthorizationId))
                {
                    await scope.Services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>().HandleAsync(validation);
                }
                if (!validation.IsRejected && consume is not null)
                {
                    await consume(scope.Services);
                }
                if (!AdministratorNativeToken.IsCurrent(bound, scope.Owner.Clock))
                {
                    validation.Reject(OpenIddictConstants.Errors.InvalidToken);
                }
            }
            await transaction.CommitAsync(cancellation);
            return bound;
        });
        if (!AdministratorNativeToken.IsCurrent(expiration, clock))
        {
            validation.Reject(OpenIddictConstants.Errors.InvalidToken);
        }
    }

    /// <summary>Checks the authoritative fixed registration and exact native token bindings before native replay revocation can write.</summary>
    private static async ValueTask<DateTimeOffset?> ExpirationAsync(ProtocolStorageScope scope, OpenIddictServerEvents.ValidateTokenContext context,
        CancellationToken cancellation)
    {
        var profile = await scope.Owner.RunAsync(token => new ValueTask<AdministratorProtocolProfile?>(scope.Owner.Database
            .Set<AdministratorProtocolProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == AdministratorProtocolProfile.SingletonId, token)), cancellation);
        var principal = context.Principal;
        if (profile is not { Enabled: true } || principal is null ||
            !string.Equals(context.TokenId, principal.GetTokenId(), StringComparison.Ordinal) ||
            !string.Equals(context.AuthorizationId, principal.GetAuthorizationId(), StringComparison.Ordinal) ||
            principal.GetPresenters() is not [AdministratorProtocolIdentity.ClientId])
        {
            return null;
        }
        var type = principal.GetTokenType();
        if (type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken &&
            principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) != OpenIddictConstants.RequestTokenTypes.Private.PushedAuthorizationRequest)
        {
            return null;
        }
        if (type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode && string.IsNullOrEmpty(context.AuthorizationId))
        {
            return null;
        }
        return type is OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode or OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
            ? await AdministratorNativeToken.ExpirationAsync(scope, principal, type, profile.ApplicationId, cancellation) : null;
    }

    /// <summary>Selects only the fixed client's protected code/request principals; other profiles retain their native handler.</summary>
    internal static bool IsAdministrator(ClaimsPrincipal? principal) =>
        principal is not null && principal.GetPresenters() is [AdministratorProtocolIdentity.ClientId] &&
        principal.GetTokenType() is OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode or OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken;
}
