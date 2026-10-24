using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves real password/MFA, fixed publication and cross-route denial over both native providers and actual Razor HTTP.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIssuerJourneyTests(BootstrapFixture fixture)
{
    private const int SessionsAfterLogin = 2;
    private const string WrongPassword = "synthetic-wrong-password";
    private const string CompleteEvent = "administrator-browser-complete";
    private const string ResumePath = "/bff/login/resume";

    /// <summary>A fresh issuer session still needs password and unused OTP; wrong-password retry preserves original admission and fixed return.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FreshIssuerCookie_When_BrowserSignInRetriesPassword_Then_NewPasswordAndMfaBindOneOriginalIntent(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var prior = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        var cookies = new CookieContainer();
        await host.SignInAsync(prior, cookies);
        using var browser = host.Browser(cookies);
        await RequireExistingSessionAsync(browser, cookies, host.Issuer);
        var intent = IssuerContinuationProviderFixture.Selector();
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        using var wrong = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry, WrongPassword);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var original = await ContinuationAsync(scenario, intent);
        Assert.Null(original.InteractionId);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        var admitted = await ContinuationAsync(scenario, intent);
        Assert.Equal(original.CreatedAt, admitted.CreatedAt);
        Assert.Equal(original.ExpiresAt, admitted.ExpiresAt);
        Assert.Equal(original.CorrelationId, admitted.CorrelationId);
        await RequirePasswordReplayDenialAsync(browser, scenario, entry);
        form[AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        using var completed = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        await RequireCompletionAsync(scenario, intent, completed, prior.SessionId);
        var after = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var replay = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Null(replay.Headers.Location);
        Assert.True(after == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>A bound B3 interaction cannot be completed through the query-free standalone form; denial leaves its valid OTP available for its actual page.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_BrowserBoundInteraction_When_SubmittedToStandaloneMfa_Then_NoOtpOrStateIsConsumed(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var prior = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        using var browser = host.Browser();
        var intent = IssuerContinuationProviderFixture.Selector();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        form[AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        var standalone = new Dictionary<string, string>(form, StringComparer.Ordinal);
        standalone.Remove(BrowserIssuerHttp.Intent);
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var denied = await BrowserIssuerHttp.PostAsync(browser, AdministratorJourney.Mfa, standalone);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        using var completed = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        await RequireCompletionAsync(scenario, intent, completed, prior.SessionId);
    }

    /// <summary>A bound pending MFA cannot become another password attempt; denial preserves its durable state and publishes no new issuer authority.</summary>
    private static async Task RequirePasswordReplayDenialAsync(HttpClient browser, JourneyScenario scenario, Dictionary<string, string> entry)
    {
        const string SetCookie = "Set-Cookie";
        const string Assignment = "=";
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var replay = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Null(replay.Headers.Location);
        Assert.False(replay.Headers.TryGetValues(SetCookie, out var cookies) &&
            cookies.Any(value => value.StartsWith(AdministratorJourney.SessionCookie + Assignment, StringComparison.Ordinal)));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Proves the pre-existing native cookie actually authorizes its protected page before testing that browser login requires new proofs.</summary>
    private static async Task RequireExistingSessionAsync(HttpClient browser, CookieContainer cookies, Uri issuer)
    {
        Assert.NotNull(cookies.GetCookies(issuer)[AdministratorJourney.SessionCookie]);
        using var protectedPage = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, protectedPage.StatusCode);
    }

    /// <summary>Reads durable continuation through an independent disposed context, never an in-memory grant substitute.</summary>
    internal static async Task<IssuerContinuation> ContinuationAsync(JourneyScenario scenario, string intent)
    {
        await using var scope = scenario.CreateScope();
        var digest = IssuerContinuationProviderFixture.Digest(intent);
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>().AsNoTracking()
            .SingleAsync(row => row.IntentDigest == digest, TestContext.Current.CancellationToken);
    }

    /// <summary>Checks the fixed single-selector redirect against actual committed completion/session/audit, without exposing selector values in assertions.</summary>
    private static async Task RequireCompletionAsync(JourneyScenario scenario, string intent, HttpResponseMessage response, string previous)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.True(location is { IsAbsoluteUri: true } && location.GetLeftPart(UriPartial.Path) == new Uri(new Uri(BrowserIssuerHost.Client), ResumePath).AbsoluteUri);
        var query = QueryHelpers.ParseQuery(location!.Query);
        Assert.True(query.Count == 1 && query[BrowserIssuerHttp.Completion].Count == 1);
        var row = await ContinuationAsync(scenario, intent);
        Assert.True(row.State == IssuerContinuationProviderFixture.Completed && row.CompletionDigest == IssuerContinuationProviderFixture.Digest(query[BrowserIssuerHttp.Completion][0]!));
        Assert.True(row.SessionId is not null && row.SessionId != previous);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(audit => audit.Event == CompleteEvent, TestContext.Current.CancellationToken));
        Assert.Equal(SessionsAfterLogin, await database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
    }
}
