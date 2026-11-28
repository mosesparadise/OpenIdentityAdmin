using System.Security.Claims;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

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

    /// <summary>Verifies the actual native access-token clone, its complete claim vocabulary, identity and credential deadline.</summary>
    internal static bool Prepared(OpenIddictServerEvents.ProcessSignInContext context, MachineGrant grant)
    {
        var principal = context.AccessTokenPrincipal;
        return principal?.Identities.Count() == 1 && principal.Identity is ClaimsIdentity { IsAuthenticated: true, Actor: null, AuthenticationType: MachineClientRules.Machine } &&
            ManagedPrincipal.MultiplicityValid(principal) && principal.Claims.All(claim => claim.Type is Claims.Subject or MachineClientRules.IdentityType or
                Claims.Private.TokenId or Claims.Private.CreationDate or Claims.Private.ExpirationDate or Claims.Private.Issuer or Claims.ClientId or
                Claims.Private.Presenter or Claims.Private.Scope or Claims.Private.Audience) &&
            principal.Claims.All(claim => claim.GetDestinations().IsEmpty) &&
            principal.GetClaim(Claims.Subject) == grant.Subject && principal.GetClaim(MachineClientRules.IdentityType) == MachineClientRules.Machine &&
            principal.GetClaim(Claims.Private.Issuer) == (context.Options.Issuer ?? context.BaseUri)!.AbsoluteUri &&
            principal.GetClaim(Claims.ClientId) == grant.Registration.ClientId && principal.GetPresenters() is [var client] && client == grant.Registration.ClientId &&
            principal.GetAudiences().SequenceEqual(grant.Resources, StringComparer.Ordinal) && principal.GetResources().IsEmpty &&
            principal.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals(grant.Scopes) && principal.GetCreationDate() is { } created &&
            principal.GetExpirationDate() is { } expires && expires > created && expires > context.Options.TimeProvider.GetUtcNow() &&
            expires.ToUnixTimeSeconds() <= grant.Registration.ExpiresAt;
    }
}
