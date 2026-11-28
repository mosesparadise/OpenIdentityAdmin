using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Devices;

/// <summary>Applies the committed-intent human decision to the original native pair under the shared ceremony transaction.</summary>
internal static class DeviceHumanDecision
{
    /// <summary>Reauthenticates the protected user reference and grants precisely the subject and scopes approved by the managed flow.</summary>
    internal static async ValueTask CompleteAsync(ProtocolStorageScope scope, ManagedHumanRequest request, ManagedLoginInteraction row, CancellationToken token)
    {
        var native = await DeviceNative.CreateAsync(scope, OpenIddictServerEndpointType.EndUserVerification, new()
        {
            UserCode = request.DeviceUserCode
        });
        var user = await native.UserAsync(token) ?? throw ProtocolStorageOwner.Unavailable();
        if (row.DeviceEntryId is null || row.UserId is null || row.ConsentVerifiedAt is null || request.DeviceUserCode is null ||
            ManagedDeviceInteraction.Demand(user, request.DeviceUserCode) != row.DemandDigest || user.GetTokenId() != row.RequestId ||
            user.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId) != row.DeviceEntryId)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (row.State == ManagedLoginState.Denied)
        {
            await native.DenyAsync(token);
            await DeviceGrantState.DecisionAsync(scope, row, accepted: false, token);
            return;
        }
        if (row.State != ManagedLoginState.DeviceApproved)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var decision = ManagedScopeBinding.Read(row);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).SetClaim(OpenIddictConstants.Claims.Subject, row.UserId)
            .SetScopes(decision.Granted).SetResources(decision.Resources);
        var signIn = await native.SignInAsync(principal, token);
        if (signIn.DeviceCodePrincipal?.GetTokenId() != row.DeviceEntryId || signIn.DeviceCodePrincipal.GetClaim(OpenIddictConstants.Claims.Subject) != row.UserId ||
            signIn.DeviceCodePrincipal.GetExpirationDate()?.ToUnixTimeSeconds() != row.NativeDependencyExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await DeviceGrantState.DecisionAsync(scope, row, accepted: true, token);
    }
}
