using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Exercises actual purpose-protected browser entry envelopes with fixed time and independent issuer browser cookies.</summary>
public sealed class IssuerBrowserProofTests
{
    private const int FractionalMilliseconds = 500;
    private const string Assignment = "=";
    private const string CookieSeparator = "; ";
    private const int LifetimeSeconds = 300;
    private const int LateSeconds = 299;
    private const string BrowserCookie = "__Host-identity-admin-issuer-browser";
    private const string EntryPrefix = "__Host-identity-admin-issuer-entry-";

    /// <summary>Repeated entry rendering keeps the original protected cookie and deadline, including a near-expiry visit.</summary>
    [Fact]
    public void Given_OriginalEntry_When_GetRepeatsLate_Then_NoProofOrDeadlineIsReplaced()
    {
        var clock = new BffSessionClock(BffSessionFixture.Start);
        var proofs = new BrowserAccountProof(new EphemeralDataProtectionProvider(), clock);
        var intent = IssuerContinuationPolicy.CreateSelector();
        var first = new DefaultHttpContext();
        var original = proofs.Prepare(first, intent);
        Assert.NotNull(original);
        var repeat = WithCookies(first);
        clock.Set(BffSessionFixture.Start.AddSeconds(LateSeconds));
        var repeated = proofs.Prepare(repeat, intent);
        Assert.NotNull(repeated);
        Assert.True(original.Request!.IntentDigest == repeated.Request!.IntentDigest && original.Request.BrowserDigest == repeated.Request.BrowserDigest);
        Assert.Equal(original.Expires, repeated.Expires);
        Assert.Equal(0, repeat.Response.Headers.SetCookie.Count);
        clock.Set(BffSessionFixture.Start.AddSeconds(LifetimeSeconds));
        Assert.Null(proofs.Prepare(repeat, intent));
        Assert.Equal(0, repeat.Response.Headers.SetCookie.Count);
    }

    /// <summary>A separate intent keeps the same issuer-browser identity, preventing repeated entry cookies from bypassing durable browser quotas.</summary>
    [Fact]
    public void Given_StableIssuerBrowser_When_AnotherIntentIsRendered_Then_QuotaIdentityRemainsTheSame()
    {
        var proofs = new BrowserAccountProof(new EphemeralDataProtectionProvider(), new BffSessionClock(BffSessionFixture.Start));
        var first = new DefaultHttpContext();
        var original = proofs.Prepare(first, IssuerContinuationPolicy.CreateSelector());
        var second = WithCookies(first);
        var another = proofs.Prepare(second, IssuerContinuationPolicy.CreateSelector());
        Assert.NotNull(original);
        Assert.NotNull(another);
        Assert.True(original.Request!.BrowserDigest == another.Request!.BrowserDigest && original.Request.IntentDigest != another.Request.IntentDigest);
    }

    /// <summary>Purpose substitution, changed intent, damaged protection and another browser cannot reuse a protected entry.</summary>
    [Theory]
    [InlineData(IssuerProofFault.OtherIntent)]
    [InlineData(IssuerProofFault.OtherBrowser)]
    [InlineData(IssuerProofFault.Purpose)]
    [InlineData(IssuerProofFault.Damaged)]
    [InlineData(IssuerProofFault.MissingBrowser)]
    public void Given_ProtectedEntry_When_ProofBindingIsChanged_Then_NoNewEntryOrAuthorityIsCreated(IssuerProofFault fault)
    {
        var proofs = new BrowserAccountProof(new EphemeralDataProtectionProvider(), new BffSessionClock(BffSessionFixture.Start));
        var intent = IssuerContinuationPolicy.CreateSelector();
        var first = new DefaultHttpContext();
        Assert.NotNull(proofs.Prepare(first, intent));
        var request = WithCookies(first);
        var values = request.Request.Cookies.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var key = EntryPrefix + IssuerContinuationPolicy.Digest(intent);
        if (fault == IssuerProofFault.OtherIntent)
        {
            intent = IssuerContinuationPolicy.CreateSelector();
        }
        if (fault == IssuerProofFault.OtherBrowser)
        {
            var other = new DefaultHttpContext();
            Assert.NotNull(proofs.Prepare(other, IssuerContinuationPolicy.CreateSelector()));
            values[BrowserCookie] = WithCookies(other).Request.Cookies[BrowserCookie]!;
        }
        if (fault == IssuerProofFault.Purpose)
        {
            values[key] = values[BrowserCookie];
        }
        if (fault == IssuerProofFault.Damaged)
        {
            values[key] = IssuerContinuationPolicy.CreateSelector();
        }
        if (fault == IssuerProofFault.MissingBrowser)
        {
            values.Remove(BrowserCookie);
        }
        request.Request.Headers.Cookie = string.Join(CookieSeparator, values.Select(pair => pair.Key + Assignment + pair.Value));
        Assert.Null(proofs.Read(request, intent));
        Assert.Equal(0, request.Response.Headers.SetCookie.Count);
    }

    /// <summary>The actual envelope cookie rounds remaining duration down so fractional time cannot extend its authenticated deadline.</summary>
    [Fact]
    public void Given_FractionalCreationTime_When_EntryCookieIsPublished_Then_MaxAgeEndsBeforeProtectedExpiry()
    {
        var clock = new BffSessionClock(BffSessionFixture.Start.AddMilliseconds(FractionalMilliseconds));
        var proofs = new BrowserAccountProof(new EphemeralDataProtectionProvider(), clock);
        var context = new DefaultHttpContext();
        Assert.NotNull(proofs.Prepare(context, IssuerContinuationPolicy.CreateSelector()));
        var entry = Assert.Single(context.Response.Headers.SetCookie, value => value!.StartsWith(EntryPrefix, StringComparison.Ordinal));
        Assert.Contains("max-age=299", entry, StringComparison.Ordinal);
    }

    /// <summary>Transfers only native response cookies into a new request without altering the protected bytes.</summary>
    private static DefaultHttpContext WithCookies(HttpContext response)
    {
        var request = new DefaultHttpContext();
        request.Request.Headers.Cookie = string.Join(CookieSeparator, response.Response.Headers.SetCookie.Select(cookie => cookie!.Split(';')[0]));
        return request;
    }
}

/// <summary>Names independent entry-envelope substitution boundaries.</summary>
public enum IssuerProofFault
{
    OtherIntent, OtherBrowser, Purpose, Damaged, MissingBrowser
}
