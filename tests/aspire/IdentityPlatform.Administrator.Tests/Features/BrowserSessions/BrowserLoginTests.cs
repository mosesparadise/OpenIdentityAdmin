using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises inert entry, protected start and one-use native resume through actual HTTP and both durable providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginTests(BootstrapFixture fixture)
{
    private const string HealthPath = "/health";
    private const string MissingPath = "/unrelated";
    private const string OriginFault = "origin";
    private const string CsrfFault = "csrf";
    private const string ExtraFault = "extra";
    private const string DuplicateFault = "duplicate";
    private const string InvalidToken = "invalid";
    private const string ReturnField = "ReturnUrl";
    private const string ForeignReturn = "https://other.example.test/";
    private const string ForeignOrigin = "https://other.example.test";
    private const string LoginPath = "/bff/login";
    private const string CookiePath = "/";
    private const string SetCookie = "Set-Cookie";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly string[] FormFaults = [OriginFault, CsrfFault, ExtraFault, DuplicateFault];
    private const string ProviderTrait = "Provider";
    private const string Origin = "Origin";

    /// <summary>Ordinary routes remain outside native ownership; entry issues no durable intent, and stale antiforgery cannot advance the committed generation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ExplicitBrowserHost_When_EntryAndProtectedStartRun_Then_OnlyThePostCreatesOneIntent(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var before = await scenario.SnapshotAsync();
        using var health = await scenario.Browser.GetAsync(HealthPath, TestContext.Current.CancellationToken);
        using var missing = await scenario.Browser.GetAsync(MissingPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.False(health.Headers.Contains(SetCookie) || missing.Headers.Contains(SetCookie));
        Assert.Equal(0, scenario.Host.Bridge.MetadataSends);
        var token = await scenario.EntryAsync();
        Assert.Equal(before, await scenario.SnapshotAsync());
        _ = await scenario.StartAsync(token);
        var committed = await scenario.SnapshotAsync();
        using var repeated = await scenario.PostAsync(token);
        RequireDenied(repeated, HttpStatusCode.BadRequest);
        Assert.Equal(committed, await scenario.SnapshotAsync());
        var intent = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BrowserIntentState.Pending, intent.State);
        Assert.Equal(1, intent.Generation);
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>Exact-origin/native-CSRF/form constraints reject before any durable mutation or native transport.</summary>
    [Theory, MemberData(nameof(Forms))]
    public async Task Given_ProtectedEntry_When_StartFormIsHostile_Then_StateAndCookiesRemainUnchanged(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var token = await scenario.EntryAsync();
        var before = await scenario.SnapshotAsync();
        var fields = new List<KeyValuePair<string, string>> { new(AdministratorJourney.Antiforgery, fault == CsrfFault ? InvalidToken : token) };
        if (fault == ExtraFault)
        {
            fields.Add(new(ReturnField, ForeignReturn));
        }
        if (fault == DuplicateFault)
        {
            fields.Add(new(AdministratorJourney.Antiforgery, token));
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginPath) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add(Origin, fault == OriginFault ? ForeignOrigin : scenario.Profile.ClientUri.GetLeftPart(UriPartial.Authority));
        using var response = await scenario.Browser.SendAsync(request, TestContext.Current.CancellationToken);
        RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
        _ = await scenario.StartAsync(token);
    }

    /// <summary>A protected pending intent cannot resume at its original expiry, even with a canonical completion selector.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_PendingIntent_When_ResumeReachesOriginalExpiry_Then_NoChallengeOrStateIsCreated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        var row = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        var before = await scenario.SnapshotAsync();
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(row.ExpiresAt, TimeSpan.Zero);
        using var denied = await scenario.ResumeAsync(completion);
        RequireDenied(denied, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>An authentic pending envelope from another browser cannot replace this browser's independent protected generation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_TwoBrowserContexts_When_PendingCookieIsSubstituted_Then_ResumeDeniesWithoutReservation(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        var otherCookies = new CookieContainer();
        using var other = scenario.Host.Browser(otherCookies);
        _ = await scenario.StartAsync(await scenario.EntryAsync(other), other);
        var pending = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Prelogin]!;
        otherCookies.Add(scenario.Profile.ClientUri, new Cookie(pending.Name, pending.Value, CookiePath) { Secure = true });
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.ResumeAsync(completion, other);
        RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Restoring both former authentic cookies cannot revive an issuer completion after the durable browser generation advances.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ReplacedBrowserGeneration_When_OriginalCookiesAndCompletionReturn_Then_NoNativeReservationIsCreated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        var generation = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value;
        var pending = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Prelogin]!.Value;
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        scenario.Cookies.Add(scenario.Profile.ClientUri, new Cookie(BrowserSessionCookies.Generation, generation, CookiePath) { Secure = true });
        scenario.Cookies.Add(scenario.Profile.ClientUri, new Cookie(BrowserSessionCookies.Prelogin, pending, CookiePath) { Secure = true });
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.ResumeAsync(completion);
        RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Requires a finite safe denial with no credential redirect or newly published cookie.</summary>
    internal static void RequireDenied(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(NoReferrer, Assert.Single(response.Headers.GetValues(ReferrerPolicy)));
    }

    /// <summary>Supplies exact provider traits to portable native discovery.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => ProviderNames.Select(provider =>
        new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));

    /// <summary>Discovers each independent protected mutation fault on both databases.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Forms => from provider in ProviderNames
                                                                      from fault in FormFaults
                                                                      select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
