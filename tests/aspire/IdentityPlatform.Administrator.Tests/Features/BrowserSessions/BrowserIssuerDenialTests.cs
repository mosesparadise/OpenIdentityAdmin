using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual HTTP input and cross-ceremony denials before any durable password/OTP authority is spent.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIssuerDenialTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string Extra = "return";
    private const string Attacker = "https://other.example.test";
    private const string Query = "?Intent=untrusted";
    private const string MalformedIntent = "?Intent=invalid";
    private const string LowercaseIntent = "?intent=";
    private const string IntentQuery = "?Intent=";
    private const string ExtraReturn = "&return=untrusted";
    private const string DuplicateIntent = "&Intent=";

    /// <summary>Retains explicit native provider metadata for each independent HTTP mutation boundary.</summary>
    public static IEnumerable<TheoryDataRow<string, BrowserPasswordFault>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Enum.GetValues<BrowserPasswordFault>()
        select new TheoryDataRow<string, BrowserPasswordFault>(provider, fault).WithTrait(ProviderTrait, provider);

    /// <summary>Missing CSRF, cross-origin/browser, duplicate/extra/query and substituted intent requests cannot admit a continuation or password interaction.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_UnboundBrowserPasswordRequest_When_PostedToActualPage_Then_AllDurableAuthorityRemainsUnchanged(string provider, BrowserPasswordFault fault)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        using var browser = host.Browser();
        using var other = host.Browser();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, IssuerContinuationProviderFixture.Selector());
        var values = new List<KeyValuePair<string, string>>(entry)
        {
            KeyValuePair.Create(BrowserIssuerHttp.Username, scenario.Lease.Username),
            KeyValuePair.Create(BrowserIssuerHttp.Password, scenario.Lease.Password)
        };
        if (fault == BrowserPasswordFault.MissingCsrf)
        {
            values.RemoveAll(pair => pair.Key == AdministratorJourney.Antiforgery);
        }
        if (fault == BrowserPasswordFault.Duplicate)
        {
            values.Add(values.Single(pair => pair.Key == BrowserIssuerHttp.Intent));
        }
        if (fault == BrowserPasswordFault.Extra)
        {
            values.Add(KeyValuePair.Create(Extra, Attacker));
        }
        if (fault == BrowserPasswordFault.Intent)
        {
            values.RemoveAll(pair => pair.Key == BrowserIssuerHttp.Intent);
            values.Add(KeyValuePair.Create(BrowserIssuerHttp.Intent, IssuerContinuationProviderFixture.Selector()));
        }
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var response = await BrowserIssuerHttp.PostAsync(fault == BrowserPasswordFault.OtherBrowser ? other : browser,
            BrowserIssuerHttp.Login + (fault == BrowserPasswordFault.Query ? Query : string.Empty), values,
            fault == BrowserPasswordFault.Origin ? Attacker : null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Malformed GET locators never admit database state or reflect arbitrary return URLs through the compiled Razor routes.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_MalformedEntryLocator_When_ActualGetIsRequested_Then_NoDurableAdmissionOccurs(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        using var browser = host.Browser();
        var intent = IssuerContinuationProviderFixture.Selector();
        var queries = new[] { string.Empty, MalformedIntent, LowercaseIntent + intent, IntentQuery + intent + ExtraReturn,
            IntentQuery + intent + DuplicateIntent + intent };
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        foreach (var query in queries)
        {
            using var response = await browser.GetAsync(BrowserIssuerHttp.Login + query, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            BrowserFormHeaders.Referrer(response);
            Assert.Null(response.Headers.Location);
        }
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>A different valid entry envelope cannot substitute its intent for an already password-bound interaction in the same browser.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AnotherProtectedIntent_When_MfaInteractionIsSubstituted_Then_OriginalCodeAndStateRemainAvailable(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        using var browser = host.Browser();
        var intent = IssuerContinuationProviderFixture.Selector();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        var other = await BrowserIssuerHttp.EntryAsync(browser, IssuerContinuationProviderFixture.Selector());
        var forged = new Dictionary<string, string>(form, StringComparer.Ordinal)
        {
            [BrowserIssuerHttp.Intent] = other[BrowserIssuerHttp.Intent],
            [AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario)
        };
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var denied = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, forged);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        form[AdministratorJourney.CodeField] = forged[AdministratorJourney.CodeField];
        using var accepted = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.True((await BrowserIssuerJourneyTests.ContinuationAsync(scenario, intent)).State == IssuerContinuationPolicy.Completed);
    }
}

/// <summary>Names independently invalid password request bindings and exact form shapes.</summary>
public enum BrowserPasswordFault
{
    MissingCsrf, Origin, OtherBrowser, Duplicate, Extra, Query, Intent
}
