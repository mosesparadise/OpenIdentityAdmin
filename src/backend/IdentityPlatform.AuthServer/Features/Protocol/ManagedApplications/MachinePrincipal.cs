using System.Security.Claims;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Builds an application principal with only the authorized API resources, machine subject and access-token destinations.</summary>
internal static class MachinePrincipal
{
    /// <summary>Uses an explicit identity type and a stable native registration subject without introducing a user, roles or human claims.</summary>
    internal static ClaimsPrincipal Create(MachineGrant grant)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(MachineClientRules.Machine));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, grant.Subject);
        principal.SetClaim(MachineClientRules.IdentityType, MachineClientRules.Machine);
        principal.SetScopes(grant.Scopes).SetResources(grant.Resources).SetPresenters(grant.Registration.ClientId);
        principal.SetDestinations(claim => claim.Type is OpenIddictConstants.Claims.Subject or MachineClientRules.IdentityType
            ? [OpenIddictConstants.Destinations.AccessToken] : []);
        return principal;
    }
}
