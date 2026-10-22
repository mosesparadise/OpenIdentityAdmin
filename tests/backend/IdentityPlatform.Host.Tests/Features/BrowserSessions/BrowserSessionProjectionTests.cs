using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks native output substitution, identity bounds and isolation of the newly created local principal.</summary>
public sealed class BrowserSessionProjectionTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Duplicate = "duplicate";
    private const string Alias = "alias";
    private const string WrongType = "type";
    private const string Actor = "actor";
    private const string Second = "second";
    private const string Anonymous = "anonymous";
    private const string AliasClaim = "SUB";
    private const string ActorName = "synthetic-actor";
    private const string ExtraRole = "synthetic-extra";
    private const string PrivateItem = "synthetic-private-item";
    private const string PrivateValue = "synthetic-value";

    /// <summary>Identical-value native output mutations must not pass through convenience-accessor normalization.</summary>
    [Theory]
    [InlineData(Duplicate)]
    [InlineData(Alias)]
    [InlineData(WrongType)]
    [InlineData(Actor)]
    [InlineData(Second)]
    [InlineData(Anonymous)]
    public async Task Given_NativePrincipal_When_StructureIsSubstituted_Then_NoCanonicalIdentityIsCreated(string fault)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture);
        var principal = token.Principal;
        var identity = Assert.IsAssignableFrom<ClaimsIdentity>(principal.Identity);
        Assert.NotNull(BrowserSessionIdentity.Validate(principal, token.Encoded, BrowserSessionToken.Intent, token.Now));
        if (fault == Duplicate)
        {
            identity.AddClaim(new(BrowserSessionToken.SubjectClaim, BrowserSessionToken.Subject));
        }
        if (fault == Alias)
        {
            identity.AddClaim(new(AliasClaim, BrowserSessionToken.Subject));
        }
        if (fault == WrongType)
        {
            var expiry = Assert.Single(identity.FindAll(BrowserSessionToken.SourceExpiryClaim));
            identity.RemoveClaim(expiry);
            identity.AddClaim(new(expiry.Type, expiry.Value, ClaimValueTypes.String));
        }
        if (fault == Actor)
        {
            identity.Actor = new ClaimsIdentity(ActorName);
        }
        if (fault == Second)
        {
            principal.AddIdentity(new ClaimsIdentity());
        }
        if (fault == Anonymous)
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity(identity.Claims));
        }
        Assert.Null(BrowserSessionIdentity.Validate(principal, token.Encoded, BrowserSessionToken.Intent, token.Now));
    }

    /// <summary>Canonical identity bounds match the existing B1 storage contract without truncation or normalization.</summary>
    [Theory]
    [InlineData(BrowserSessionToken.SubjectClaim, 400, true)]
    [InlineData(BrowserSessionToken.SubjectClaim, 401, false)]
    [InlineData(BrowserSessionToken.SessionClaim, 400, true)]
    [InlineData(BrowserSessionToken.SessionClaim, 401, false)]
    public async Task Given_SignedIdentityBinding_When_AtStorageLimit_Then_ExactBoundApplies(string name, int length, bool accepted)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture,
            payload => BrowserSessionToken.Field(payload, name, JsonSerializer.Serialize(new string('A', length))));
        var binding = BrowserSessionIdentity.Validate(token.Principal, token.Encoded, BrowserSessionToken.Intent, token.Now);
        Assert.Equal(accepted, binding is not null);
    }

    /// <summary>Projection is immutable and repeatable: modifying one output cannot change the native identity or a subsequent local ticket.</summary>
    [Fact]
    public async Task Given_ValidNativeIdentity_When_LocalTicketIsMutated_Then_InputAndNextProjectionRemainIsolated()
    {
        var token = await BrowserSessionToken.CreateAsync(fixture);
        var original = token.Principal.Claims.Select(claim => (claim.Type, claim.Value, claim.ValueType)).ToArray();
        var binding = Assert.IsType<BrowserSessionIdentity>(BrowserSessionIdentity.Validate(token.Principal, token.Encoded, BrowserSessionToken.Intent, token.Now));
        var first = binding.CreateTicket();
        Assert.Empty(first.Properties.Parameters);
        Assert.Single(first.Properties.Items);
        Assert.IsType<ClaimsIdentity>(first.Principal.Identity).AddClaim(new(BrowserSessionToken.RoleClaim, ExtraRole));
        first.Properties.Items[PrivateItem] = PrivateValue;
        var second = binding.CreateTicket();
        Assert.Equal(2, second.Principal.Claims.Count());
        Assert.Single(second.Properties.Items);
        Assert.Equal(original, token.Principal.Claims.Select(claim => (claim.Type, claim.Value, claim.ValueType)).ToArray());
    }
}
