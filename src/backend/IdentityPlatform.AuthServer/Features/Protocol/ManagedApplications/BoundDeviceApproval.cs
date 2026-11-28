using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Preserves the originally registered device window when native verification replaces the inactive device payload.</summary>
internal sealed class BoundDeviceApproval(ProtocolStorageOwner owner) : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Bounds the prepared principal before OpenIddict protects or stores the approved device code.</summary>
    internal static void Register(OpenIddictServerBuilder server)
    {
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .AddFilter<OpenIddictServerHandlerFilters.RequireEndUserVerificationRequest>().UseScopedHandler<LockDeviceApproval>()
            .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order - 1));
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .AddFilter<OpenIddictServerHandlerFilters.RequireEndUserVerificationRequest>().UseScopedHandler<BoundDeviceApproval>()
            .SetOrder(OpenIddictServerHandlers.PrepareDeviceCodePrincipal.Descriptor.Order + 1));
    }

    /// <summary>Requires a current native user-code authentication, the same subject and a live retained window in the original write scope.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.EndpointType != OpenIddictServerEndpointType.EndUserVerification)
        {
            return;
        }
        var (storage, user) = RequireVerification(context, owner);
        if (context.DeviceCodePrincipal is not { } device || context.Principal is not { Identity.IsAuthenticated: true } principal ||
            principal.GetClaim(OpenIddictConstants.Claims.Subject) is not { Length: > 0 } subject ||
            device.GetClaim(OpenIddictConstants.Claims.Subject) != subject || device.GetAuthorizationId() != user.GetAuthorizationId() ||
            device.GetTokenId() != user.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId) || device.GetPresenters() is not [var clientId] ||
            clientId != user.GetClaim(OpenIddictConstants.Claims.ClientId))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var deadline = await DevicePollAdmission.ApprovalDeadlineLockedAsync(storage.Scope, user, device.GetScopes(), context.CancellationToken);
        if (device.GetCreationDate() is not { } created || created >= deadline || device.GetExpirationDate() is not { } prepared || prepared <= created)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var expires = prepared < deadline ? prepared : deadline;
        device.SetExpirationDate(expires);
        storage.Request.RestrictPublication(expires);
    }

    /// <summary>Requires the completed native user-code authentication and the original owned write scope for both stages of approval.</summary>
    internal static (ProtocolStorageAmbient Storage, ClaimsPrincipal User) RequireVerification(OpenIddictServerEvents.ProcessSignInContext context, ProtocolStorageOwner owner)
    {
        var auth = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        if (context.EndpointType != OpenIddictServerEndpointType.EndUserVerification || ProtocolStorageAmbient.Current is not { } storage ||
            !ReferenceEquals(owner, storage.Scope.Owner) || owner.Transaction is null ||
            auth is not { IsRejected: false, IsRequestHandled: false, IsRequestSkipped: false, UserCodePrincipal: { } user } ||
            !ReferenceEquals(auth.Transaction, context.Transaction) || user.GetTokenType() != OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return (storage, user);
    }
}

/// <summary>Acquires current governance authority before OpenIddict writes the redeemed user-code row.</summary>
internal sealed class LockDeviceApproval(ProtocolStorageOwner owner) : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Locks only active native device verification transactions in the managed pipeline.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.EndUserVerification)
        {
            var (storage, _) = BoundDeviceApproval.RequireVerification(context, owner);
            await ManagedRegistration.LockAsync(storage.Scope, context.CancellationToken);
        }
    }
}
