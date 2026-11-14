using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Checks that the selected managed identity cannot acquire administrator, delegation or broader token projection claims.</summary>
public sealed class ManagedPrincipalTests
{
    private const string Client = "managed-client";
    private const string Subject = "synthetic-subject";
    private const string Selector = "synthetic-selector";
    private const string Administrator = "identity.admin";
    private const string Foreign = "foreign";

    /// <summary>Extra roles, scopes, audiences, actors, identities or subject destinations cannot satisfy the identity-only principal contract.</summary>
    [Theory]
    [InlineData(ManagedPrincipalDefect.Valid)]
    [InlineData(ManagedPrincipalDefect.Role)]
    [InlineData(ManagedPrincipalDefect.Scope)]
    [InlineData(ManagedPrincipalDefect.Audience)]
    [InlineData(ManagedPrincipalDefect.Actor)]
    [InlineData(ManagedPrincipalDefect.Identity)]
    [InlineData(ManagedPrincipalDefect.Destination)]
    public void Given_ManagedIdentity_When_ExtraAuthorityOrDelegationIsAdded_Then_OnlyOriginalSubjectProjectionMatches(ManagedPrincipalDefect defect)
    {
        var row = new ManagedLoginInteraction { ApplicationId = Client, UserId = Subject, ApplicationRevision = 1 };
        var principal = ManagedPrincipal.Create(row, Client, Selector);
        switch (defect)
        {
            case ManagedPrincipalDefect.Role:
                principal.SetClaim(OpenIddictConstants.Claims.Role, Administrator);
                break;
            case ManagedPrincipalDefect.Scope:
                principal.SetScopes(OpenIddictConstants.Scopes.OpenId, Administrator);
                break;
            case ManagedPrincipalDefect.Audience:
                principal.SetAudiences(Administrator);
                break;
            case ManagedPrincipalDefect.Actor:
                ((ClaimsIdentity)principal.Identity!).Actor = new ClaimsIdentity(Foreign);
                break;
            case ManagedPrincipalDefect.Identity:
                principal.AddIdentity(new ClaimsIdentity(Foreign));
                break;
            case ManagedPrincipalDefect.Destination:
                principal.FindFirst(OpenIddictConstants.Claims.Subject)!.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                break;
        }
        Assert.Equal(defect == ManagedPrincipalDefect.Valid, ManagedPrincipal.Matches(principal, row, Client, Selector));
    }
}

/// <summary>Names independently forbidden claim projection and delegation changes.</summary>
public enum ManagedPrincipalDefect
{
    Valid, Role, Scope, Audience, Actor, Identity, Destination
}
