using System.Collections.Immutable;
using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Checks consent-bound resources and current email independently of native cryptographic integration coverage.</summary>
public sealed class ManagedConsentProjectionTests
{
    private const string OpenId = "openid";
    private const string EmailScope = "email";
    private const string EmailId = "email-id";
    private const string OrdersId = "orders-id";
    private const string AudienceId = "audience-id";

    private const string Client = "managed-client";
    private const string Subject = "synthetic-subject";
    private const string Selector = "synthetic-selector";
    private const string Api = "orders.read";
    private const string Resource = "orders-api";
    private const string Email = "person@example.test";

    /// <summary>A requested but ungranted API scope never creates an audience; only granted identity policy discloses email.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Given_BoundPolicy_When_GrantMembershipChanges_Then_ResourcesAndEmailFollowOnlyTheGrant(bool api, bool email)
    {
        var decision = Policy(api, email);
        Assert.Equal(api ? new[] { Resource } : [], decision.Resources);
        Assert.Equal(email, decision.DisclosesEmail);
        var row = new ManagedLoginInteraction { ApplicationId = Client, ApplicationRevision = 1, UserId = Subject };
        ManagedScopeBinding.Attach(row, decision);
        var principal = ManagedPrincipal.Create(row, Client, Selector);
        Assert.Equal(decision.Granted, principal.GetScopes());
        Assert.Equal(decision.Resources, principal.GetResources());
        Assert.True(ManagedPrincipal.Matches(principal, row, Client, Selector));
        Assert.Empty(principal.GetClaims(OpenIddictConstants.Claims.Email));
        Assert.Empty(principal.GetClaims(OpenIddictConstants.Claims.EmailVerified));
    }

    /// <summary>Only consented current nonempty email produces the complete pair, retaining the account's actual verification boolean.</summary>
    [Theory]
    [InlineData(false, Email, true)]
    [InlineData(true, null, true)]
    [InlineData(true, "", true)]
    [InlineData(true, Email, false)]
    [InlineData(true, Email, true)]
    public void Given_CurrentIdentityEmail_When_Projected_Then_ConsentAbsenceAndVerificationArePreserved(bool consent, string? address, bool verified)
    {
        var projection = ManagedEmailProjection.Read(Policy(false, consent), new PlatformUser { Email = address, EmailConfirmed = verified });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(Client));
        ManagedEmailClaims.Attach(principal, projection);
        if (!consent || string.IsNullOrEmpty(address))
        {
            Assert.Null(projection);
            Assert.Empty(principal.Claims);
            return;
        }
        Assert.Equal(new ManagedEmailProjection(address, verified), projection);
        Assert.True(ManagedEmailClaims.Matches(principal, projection, destinations: true));
        var boolean = principal.FindFirst(OpenIddictConstants.Claims.EmailVerified)!;
        Assert.Equal(ClaimValueTypes.Boolean, boolean.ValueType);
        Assert.Equal(verified ? "true" : "false", boolean.Value);
        Assert.All(principal.Claims, claim => Assert.Equal([OpenIddictConstants.Destinations.IdentityToken], claim.GetDestinations()));
        Assert.False(ManagedEmailClaims.Matches(principal, projection, destinations: false));
        Assert.Throws<InvalidOperationException>(() => ManagedEmailClaims.Attach(principal, projection));
    }

    /// <summary>Canonical policy serializes no computed projection or personal data and round-trips without changing its digest.</summary>
    [Fact]
    public void Given_CurrentEmail_When_PolicyIsEncoded_Then_PersonalDataAndComputedPropertiesAreAbsent()
    {
        var decision = Policy(true, true);
        var encoded = ManagedScopeCodec.Encode(decision);
        Assert.DoesNotContain("Resources", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("DisclosesEmail", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain(Email, encoded, StringComparison.Ordinal);
        Assert.Equal(encoded, ManagedScopeCodec.Encode(ManagedScopeCodec.Decode(encoded, ManagedLoginRules.Digest(encoded))));
    }

    /// <summary>Uses independently named synthetic metadata and canonical sorted membership, including a deliberately ungranted requested API scope.</summary>
    private static ManagedScopeDecision Policy(bool api, bool email)
    {
        var binding = ManagedLoginRules.Digest(Selector);
        return new(1, Client, 1, binding, [EmailScope, OpenId, Api],
            ImmutableArray.Create(OpenId).Concat(email ? [EmailScope] : []).Concat(api ? [Api] : []).Order(StringComparer.Ordinal).ToImmutableArray(),
            [new(EmailId, EmailScope, 1, CatalogueRules.Identity, CatalogueRules.Email, binding),
                new(OrdersId, Api, 1, CatalogueRules.Api, CatalogueRules.None, binding)], new(AudienceId, Resource, 1, binding));
    }

    /// <summary>The native multi-scope shape must not admit duplicates, case aliases, nonstring scopes or repeated unrelated private claims.</summary>
    [Theory]
    [InlineData(ConsentMultiplicity.Valid)]
    [InlineData(ConsentMultiplicity.Duplicate)]
    [InlineData(ConsentMultiplicity.CaseAlias)]
    [InlineData(ConsentMultiplicity.NonString)]
    [InlineData(ConsentMultiplicity.PrivateClaim)]
    public void Given_MultipleGrantedScopes_When_ClaimMultiplicityIsChanged_Then_OnlyDistinctNativeScopeStringsMatch(ConsentMultiplicity defect)
    {
        var row = new ManagedLoginInteraction { ApplicationId = Client, ApplicationRevision = 1, UserId = Subject };
        ManagedScopeBinding.Attach(row, Policy(true, true));
        var principal = ManagedPrincipal.Create(row, Client, Selector);
        var identity = (ClaimsIdentity)principal.Identity!;
        switch (defect)
        {
            case ConsentMultiplicity.Duplicate:
                identity.AddClaim(new(OpenIddictConstants.Claims.Private.Scope, OpenIddictConstants.Scopes.OpenId));
                break;
            case ConsentMultiplicity.CaseAlias:
                identity.AddClaim(new(OpenIddictConstants.Claims.Private.Scope.ToUpperInvariant(), OpenIddictConstants.Scopes.OpenId));
                break;
            case ConsentMultiplicity.NonString:
                var original = identity.FindFirst(OpenIddictConstants.Claims.Private.Scope)!;
                identity.RemoveClaim(original);
                identity.AddClaim(new(original.Type, original.Value, ClaimValueTypes.Integer32));
                break;
            case ConsentMultiplicity.PrivateClaim:
                identity.AddClaim(new(ManagedDemand.SelectorClaim, Selector));
                break;
        }
        Assert.Equal(defect == ConsentMultiplicity.Valid, ManagedPrincipal.Matches(principal, row, Client, Selector));
    }
}

/// <summary>Names independent multiplicity defects around the one explicitly multivalued native scope field.</summary>
public enum ConsentMultiplicity
{
    Valid, Duplicate, CaseAlias, NonString, PrivateClaim
}
