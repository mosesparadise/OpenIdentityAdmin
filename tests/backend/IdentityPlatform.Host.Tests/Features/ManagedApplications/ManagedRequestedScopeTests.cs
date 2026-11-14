using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Exercises the public managed scope grammar before cache allocation, independently of catalogue assignment authority.</summary>
public sealed class ManagedRequestedScopeTests
{
    private const int AdditionalLimit = 32;
    private const string OpenId = "openid";
    private const string ScopePrefix = "scope.";

    /// <summary>Malformed delimiters, duplicates, missing OpenID and fixed-profile scopes cannot be normalized into an eligible demand.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("email")]
    [InlineData("openid openid")]
    [InlineData("openid email email")]
    [InlineData(" openid")]
    [InlineData("openid ")]
    [InlineData("openid  email")]
    [InlineData("openid\temail")]
    [InlineData("openid offline_access")]
    [InlineData("openid identity.admin")]
    [InlineData("openid pilot_api")]
    public void Given_UnsupportedScopeText_When_ParsedForManagedAdmission_Then_NoDemandIsAccepted(string? input) =>
        Assert.Null(ManagedRequestedScopes.Read(input));

    /// <summary>Requested order does not alter membership; the 32nd additional scope is allowed and the 33rd is rejected.</summary>
    [Fact]
    public void Given_BoundedUniqueScopes_When_ParsedInAnyOrder_Then_ExactMembershipAndLimitArePreserved()
    {
        var names = Enumerable.Range(0, AdditionalLimit).Select(value => ScopePrefix + value).Append(OpenId).ToArray();
        var original = ManagedRequestedScopes.Read(string.Join(' ', names));
        var reversed = ManagedRequestedScopes.Read(string.Join(' ', names.Reverse()));
        Assert.NotNull(original);
        Assert.NotNull(reversed);
        Assert.Equal(names.Order(StringComparer.Ordinal), original.Value);
        Assert.Equal(original.Value, reversed.Value);
        Assert.Null(ManagedRequestedScopes.Read(string.Join(' ', names.Append(ScopePrefix + AdditionalLimit))));
    }
}
