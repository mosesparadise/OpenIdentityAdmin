using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves the original protected entry deadline reaches actual admission/MFA owners instead of resetting at a late password POST.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIssuerExpiryTests(BootstrapFixture fixture)
{
    private const int LateSeconds = 299;
    private const int LifetimeSeconds = 300;

    /// <summary>A late actual password admission has only its original remaining second; valid MFA at equality creates no session/completion/cookie.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LateFirstPasswordPost_When_OriginalEntryExpires_Then_MfaCannotUseANewFiveMinuteWindow(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        using var browser = host.Browser();
        var intent = IssuerContinuationProviderFixture.Selector();
        var start = scenario.Clock.GetUtcNow();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        scenario.Clock.Now = start.AddSeconds(LateSeconds);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        var row = await BrowserIssuerJourneyTests.ContinuationAsync(scenario, intent);
        Assert.Equal(start.ToUnixTimeSeconds() + LifetimeSeconds, row.ExpiresAt);
        Assert.Equal(1, row.ExpiresAt - row.CreatedAt);
        form[AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        scenario.Clock.Now = start.AddSeconds(LifetimeSeconds);
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var denied = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Explicit same-entry admission reconciliation is read-only; another intent or later deadline cannot replace pending state or bypass per-browser capacity.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingOriginalEntry_When_AdmissionIsExplicitlyReconciled_Then_OnlyExactUnchangedRequestCanResume(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var attempt = IssuerContinuationProviderFixture.Create();
        var expiry = scenario.Clock.GetUtcNow().ToUnixTimeSeconds() + LifetimeSeconds;
        Assert.True(await AdmitAsync(scenario, attempt.Request, expiry));
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        Assert.True(await AdmitAsync(scenario, attempt.Request, expiry));
        Assert.False(await AdmitAsync(scenario, attempt.Request, expiry + 1));
        Assert.False(await AdmitAsync(scenario, IssuerContinuationRequest.Read(IssuerContinuationProviderFixture.Selector(), attempt.Browser)!, expiry));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        await using var scope = scenario.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>().CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Uses a fresh real-provider scope for each admission/read-only reconciliation.</summary>
    private static async Task<bool> AdmitAsync(JourneyScenario scenario, IssuerContinuationRequest request, long expires)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IssuerContinuationService>().AdmitForBrowserAsync(request, expires, TestContext.Current.CancellationToken);
    }
}
