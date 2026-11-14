using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Checks protected managed provenance against exact current native rows before native validation may write replay effects.</summary>
internal sealed class ManagedTokenBinding(ManagedRegistration registration, ManagedLoginInteraction row, DateTimeOffset expiration)
{
    internal ManagedRegistration Registration => registration;
    internal ManagedLoginInteraction Row => row;
    internal DateTimeOffset Expiration => expiration;

    /// <summary>Classifies only exact managed cached-request/code presenters; raw request client identity is independently checked.</summary>
    internal static bool Candidate(ClaimsPrincipal? principal) => principal is not null &&
        principal.GetPresenters() is [var client] && ManagedDemand.Candidate(client) &&
        principal.GetTokenType() is OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken or OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode;

    /// <summary>Requires current policy, exact native owner/subject/type/deadline and the original managed row without trusting token status.</summary>
    internal static async ValueTask<ManagedTokenBinding?> ReadAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, string client, CancellationToken token)
    {
        var selector = principal.GetClaim(ManagedDemand.SelectorClaim);
        if (!Candidate(principal) || principal.GetPresenters() is not [var presenter] || presenter != client || !ManagedLoginRules.IsSecret(selector))
        {
            return null;
        }
        var registration = await ManagedRegistration.ReadAsync(scope, client, token);
        var digest = ManagedLoginRules.Digest(selector!);
        var row = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction?>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.SelectorDigest == digest, current)), token);
        if (registration is null || row is null || row.SelectorDigest != digest || row.ApplicationId != registration.ApplicationId ||
            row.ApplicationRevision != registration.Revision || principal.GetClaim(ManagedDemand.RevisionClaim) != registration.Revision.ToString(CultureInfo.InvariantCulture) ||
            ManagedLoginRules.Observe(scope.Owner.Clock, Math.Max(row.ObservedAt, registration.ObservedAt)) >= row.ExpiresAt || !Bound(principal, row, client, selector!))
        {
            return null;
        }
        await ManagedScopeBinding.RequireOpenIdAsync(scope, row, token);
        var expiration = await AdministratorNativeToken.ExpirationAsync(scope, principal, principal.GetTokenType()!, registration.ApplicationId, token);
        return expiration is { } expires ? new(registration, row, expires) : null;
    }

    /// <summary>Separates anonymous cached-request provenance from the exact consumed interaction's identity-only native authorization code.</summary>
    private static bool Bound(ClaimsPrincipal principal, ManagedLoginInteraction row, string client, string selector) =>
        principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken)
            ? principal.GetTokenId() == row.RequestId && principal.GetAuthorizationId() is null && principal.GetClaim(OpenIddictConstants.Claims.Subject) is null &&
                principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) == OpenIddictConstants.RequestTokenTypes.Private.CachedAuthorizationRequest &&
                principal.GetExpirationDate()?.ToUnixTimeSeconds() == row.RequestExpiresAt
            : row.State == ManagedLoginState.Consumed && principal.GetTokenId() == row.CodeId && principal.GetAuthorizationId() == row.AuthorizationId &&
                ManagedPrincipal.Matches(principal, row, client, selector, protectedCode: true);
}
