using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Proves cookie expiry equality, independent purposes and secure host scope without enabling a cookie authentication fallback.</summary>
public sealed class BrowserCookiePolicyTests
{
    private const string Root = "/";
    private const string NativePrefix = "__Host-identity-admin-client-state";

    /// <summary>Expiry is truncated rather than rounded upward, and a generation proof may use its longer live ticket bound.</summary>
    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(9999999, null)]
    [InlineData(10000000, 1)]
    [InlineData(19999999, 1)]
    [InlineData(288000000000, 28800)]
    public void Given_ServerDeadline_When_ProofCookieIsPrepared_Then_NoCookieOutlivesIt(long ticks, int? seconds)
    {
        var now = BffSessionFixture.Start;
        var options = BrowserSessionCookies.Bounded(now, now.AddTicks(ticks));
        if (seconds is null)
        {
            Assert.Null(options);
            return;
        }
        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromSeconds(seconds.Value), options.MaxAge);
        Assert.True(now + options.MaxAge <= now.AddTicks(ticks));
        Scope(options);
    }

    /// <summary>Session cookies remain nonpersistent, with roles and proof purposes distinct from native client correlation and each other.</summary>
    [Fact]
    public void Given_BrowserCookieRoles_When_PoliciesAreRead_Then_HostScopeAndPurposeSeparationRemainExplicit()
    {
        var options = BrowserSessionCookies.SessionOptions();
        Scope(options);
        Assert.Null(options.MaxAge);
        Assert.Null(options.Expires);
        Assert.Equal("__Host-identity-admin-session", BrowserSessionCookies.Session);
        string[] names = [BrowserSessionCookies.Session, BrowserSessionCookies.Generation, BrowserSessionCookies.Prelogin,
            BrowserSessionCookies.Logout, BrowserSessionCookies.Antiforgery, NativePrefix];
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.StartsWith("__Host-", name, StringComparison.Ordinal));
        string[] purposes = [BrowserSessionCookies.GenerationPurpose, BrowserSessionCookies.PreloginPurpose, BrowserSessionCookies.LogoutPurpose];
        Assert.Equal(purposes.Length, purposes.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Checks each security attribute independently, including deletion-compatible host path and absent Domain.</summary>
    private static void Scope(CookieOptions options)
    {
        Assert.Equal(Root, options.Path);
        Assert.Null(options.Domain);
        Assert.True(options.Secure);
        Assert.True(options.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
    }
}
