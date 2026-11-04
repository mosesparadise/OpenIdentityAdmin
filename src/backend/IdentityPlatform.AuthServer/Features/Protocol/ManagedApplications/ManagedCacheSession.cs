using System.Globalization;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Owns one cache reservation, original authority and exact native prepared request principal.</summary>
internal sealed class ManagedCacheSession(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context,
    ManagedAdmission admission, ManagedRegistration registration, ManagedLoginReservation reservation, string selector)
{
    internal ManagedCacheProof Proof { get; } = new(scope, context);

    /// <summary>Finds only the state installed by the managed coordinator inside its actual fresh response child.</summary>
    internal static ManagedCacheSession? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<ManagedCacheSession>(typeof(ManagedCacheSession).FullName!);

    /// <summary>Checks native generation flags and bounds the original anonymous cached-request principal before any native creation.</summary>
    internal void Prepare()
    {
        if (!admission.Allows(context.Transaction, registration) || admission.Demand.Cached ||
            context.EndpointType != OpenIddictServerEndpointType.Authorization || context.Principal?.Identity?.IsAuthenticated != false ||
            !context.GenerateRequestToken || !context.IncludeRequestToken || context.GenerateAuthorizationCode || context.IncludeAuthorizationCode ||
            context.GenerateAccessToken || context.IncludeAccessToken || context.GenerateIdentityToken || context.IncludeIdentityToken ||
            context.GenerateRefreshToken || context.IncludeRefreshToken || context.GenerateDeviceCode || context.IncludeDeviceCode ||
            context.GenerateUserCode || context.IncludeUserCode || context.GenerateIssuedToken || context.IncludeIssuedToken ||
            context.RequestTokenPrincipal is not { } principal || principal.GetClaim(OpenIddictConstants.Claims.Subject) is not null ||
            principal.GetAuthorizationId() is not null || principal.GetTokenId() is not null ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) != OpenIddictConstants.RequestTokenTypes.Private.CachedAuthorizationRequest ||
            !principal.GetPresenters().SequenceEqual([registration.ClientId], StringComparer.Ordinal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        principal.SetCreationDate(DateTimeOffset.FromUnixTimeSeconds(reservation.CreatedAt));
        principal.SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(reservation.ExpiresAt));
        principal.SetClaim(ManagedDemand.SelectorClaim, selector);
        principal.SetClaim(ManagedDemand.RevisionClaim, registration.Revision.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Requires current locked profile and actual native row attachment after response sealing, immediately before commit.</summary>
    internal async ValueTask VerifyAsync(CancellationToken token)
    {
        var principal = context.RequestTokenPrincipal;
        if (!admission.Allows(context.Transaction, registration) || !await Proof.VerifyAsync(registration.ApplicationId, token) || principal is null ||
            principal.GetClaim(ManagedDemand.SelectorClaim) != selector || principal.GetClaim(ManagedDemand.RevisionClaim) != registration.Revision.ToString(CultureInfo.InvariantCulture) ||
            principal.GetCreationDate()?.ToUnixTimeSeconds() != reservation.CreatedAt || principal.GetExpirationDate()?.ToUnixTimeSeconds() != reservation.ExpiresAt ||
            principal.GetClaim(OpenIddictConstants.Claims.Subject) is not null || principal.GetAuthorizationId() is not null ||
            !registration.Matches(await ManagedRegistration.ReadAsync(scope, registration.ClientId, token)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await reservation.AttachAsync(principal.GetTokenId()!, reservation.CreatedAt, reservation.ExpiresAt, token);
        await reservation.VerifyFinalAsync(token);
        if (!await Proof.VerifyAsync(registration.ApplicationId, token) ||
            !registration.Matches(await ManagedRegistration.ReadAsync(scope, registration.ClientId, token)) ||
            ManagedLoginRules.Observe(scope.Owner.Clock, registration.ObservedAt) >= Math.Min(registration.ExpiresAt, reservation.ExpiresAt))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
