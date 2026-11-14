using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.UserInfo;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Separates original signed expiration from restored native metadata so neither lifetime can extend the other.</summary>
public sealed class UserInfoLifetimeTests
{
    private const string Expiration = "exp";
    private const string ExpirationAlias = "EXP";
    private const long Now = 100;
    private const long NativeExpiry = 300;
    private const string NearExpiry = "200";
    private const string LaterExpiry = "400";

    /// <summary>Future private/stored metadata cannot legitimize missing, invalid, overflowing, past or equality-bound public expiration.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("99")]
    [InlineData("100")]
    [InlineData("+200")]
    [InlineData("200.0")]
    [InlineData("0200")]
    [InlineData("253402300800")]
    [InlineData("9223372036854775808")]
    public void Given_RestoredFutureLifetime_When_SignedExpiryIsMissingInvalidOrExpired_Then_UserInfoHasNoLifetime(string? value)
    {
        var principal = Principal(value);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(NativeExpiry), principal.GetExpirationDate());
        Assert.Null(UserInfoLifetime.Bound(principal, DateTimeOffset.FromUnixTimeSeconds(NativeExpiry), DateTimeOffset.FromUnixTimeSeconds(Now)));
    }

    /// <summary>The earlier signed or stored bound wins; original signed fields remain intact and duplicate expiry never becomes authority.</summary>
    [Theory]
    [InlineData(NearExpiry, 200)]
    [InlineData(LaterExpiry, NativeExpiry)]
    public void Given_TwoCurrentLifetimes_When_UserInfoBoundsPublication_Then_OnlyEarlierExpirySurvives(string value, long expected)
    {
        var principal = Principal(value, value == NearExpiry ? ClaimValueTypes.Integer32 : ClaimValueTypes.Integer64);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(expected), UserInfoLifetime.Bound(principal,
            DateTimeOffset.FromUnixTimeSeconds(NativeExpiry), DateTimeOffset.FromUnixTimeSeconds(Now)));
        Assert.Equal(value, principal.GetClaim(Expiration));
        Assert.Null(UserInfoLifetime.Bound(principal, DateTimeOffset.FromUnixTimeSeconds(Now), DateTimeOffset.FromUnixTimeSeconds(Now)));
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(Expiration, value));
        Assert.Null(UserInfoLifetime.Bound(principal, DateTimeOffset.FromUnixTimeSeconds(NativeExpiry), DateTimeOffset.FromUnixTimeSeconds(Now)));
    }

    /// <summary>A numeric-looking string, floating-point value or boolean cannot substitute for the signed NumericDate integer.</summary>
    [Theory]
    [InlineData(ClaimValueTypes.String)]
    [InlineData(ClaimValueTypes.Double)]
    [InlineData(ClaimValueTypes.Boolean)]
    public void Given_WrongSignedClaimType_When_UserInfoBoundsPublication_Then_NoLifetimeIsAccepted(string type)
    {
        Assert.Null(UserInfoLifetime.Bound(Principal(NearExpiry, type), DateTimeOffset.FromUnixTimeSeconds(NativeExpiry),
            DateTimeOffset.FromUnixTimeSeconds(Now)));
    }

    /// <summary>A case-insensitive identity lookup cannot turn a nonstandard uppercase alias into the required signed exp claim.</summary>
    [Fact]
    public void Given_UppercaseExpirationAlias_When_UserInfoBoundsPublication_Then_NoLifetimeIsAccepted()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ExpirationAlias, NearExpiry, ClaimValueTypes.Integer64)]))
            .SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(NativeExpiry));
        Assert.Null(UserInfoLifetime.Bound(principal, DateTimeOffset.FromUnixTimeSeconds(NativeExpiry), DateTimeOffset.FromUnixTimeSeconds(Now)));
    }

    /// <summary>Models the actual native restoration: private expiration is later while the original signed exp claim remains unchanged.</summary>
    private static ClaimsPrincipal Principal(string? value, string type = ClaimValueTypes.Integer64)
    {
        var identity = new ClaimsIdentity();
        if (value is not null)
        {
            identity.AddClaim(new Claim(Expiration, value, type));
        }
        return new ClaimsPrincipal(identity).SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(NativeExpiry));
    }
}
