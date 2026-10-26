using System.Net;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Isolates generation and native antiforgery binding with genuine current authority and exact committed effects.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserBindingMutationTests(BootstrapFixture fixture)
{
    private const string GenerationMarker = "browser-generation-binding-bypass";
    private const string AntiforgeryMarker = "browser-antiforgery-binding-bypass";
    private const string Subject = "subject";
    private const string ExpiresAt = "expiresAt";
    private const string SetCookie = "Set-Cookie";
    private const string Authenticated = "authenticated";
    private const string CookiePath = "/";
    private const int ReplacedGeneration = 2;

    /// <summary>An authentic old generation cannot accompany a genuinely current replacement ticket; restoring the current proof remains usable.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-generation-binding")]
    public async Task Given_CurrentReplacementTicket_When_GenuineOlderGenerationIsSubstituted_Then_StatusRejectsOnlyThatBinding(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var first = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, first.StatusCode);
        var old = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value;
        using var second = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, second.StatusCode);
        var current = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value;
        Assert.True(old != current);
        var ticket = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(row => row.RevokedAt == null, TestContext.Current.CancellationToken));
        await BrowserSessionStatusTests.RequireAuthenticatedAsync(scenario, ticket);
        scenario.Cookies.Add(scenario.Profile.ClientUri, new Cookie(BrowserSessionCookies.Generation, old, CookiePath) { Secure = true });
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.Browser.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var bypass = json.RootElement.GetProperty(Authenticated).GetBoolean();
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        if (bypass)
        {
            await RequireOldProjectionAsync(scenario, json.RootElement, ticket, old);
        }
        else
        {
            Assert.Equal([Authenticated], json.RootElement.EnumerateObject().Select(item => item.Name));
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
        scenario.Cookies.Add(scenario.Profile.ClientUri, new Cookie(BrowserSessionCookies.Generation, current, CookiePath) { Secure = true });
        await BrowserSessionStatusTests.RequireAuthenticatedAsync(scenario, ticket);
        Assert.False(bypass, GenerationMarker);
    }

    /// <summary>Requires the exact older proof's capped deadline and genuine current ticket subject without imposing a newer-proof expiry on the bypass.</summary>
    private static async Task RequireOldProjectionAsync(BrowserLoginFixture scenario, JsonElement json, AdminSessionTicket ticket, string old)
    {
        using var protection = scenario.ReloadProtection();
        var proof = new BrowserLoginProof(protection.Provider).Generation(old, scenario.Issuer.Account.Journey.Clock.GetUtcNow());
        Assert.NotNull(proof);
        var browser = await scenario.ReadAsync(db => db.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.True(proof.Generation < browser.Generation && browser.TicketDigest == ticket.Digest);
        var deadlines = new[] { ticket.IdleExpiresAt, ticket.AbsoluteExpiresAt, browser.ExpiresAt, proof.Expires };
        Assert.Equal([Authenticated, ExpiresAt, Subject], json.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        Assert.True(json.GetProperty(Subject).GetString() == ticket.Subject);
        Assert.Equal(new DateTimeOffset(deadlines.Min(), TimeSpan.Zero), json.GetProperty(ExpiresAt).GetDateTimeOffset());
    }

    /// <summary>A consumed-generation native request token must not create a second intent with the current cookie; a bypass requires actual generation and audit advancement.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-antiforgery-binding")]
    public async Task Given_ValidOriginalAntiforgery_When_StartAlreadyAdvancedGeneration_Then_OldTokenCannotCommitAnotherIntent(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var token = await scenario.EntryAsync();
        _ = await scenario.StartAsync(token);
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.PostAsync(token);
        var bypass = response.StatusCode == HttpStatusCode.SeeOther;
        if (bypass)
        {
            Assert.NotNull(response.Headers.Location);
            Assert.Equal(ReplacedGeneration, await scenario.ReadAsync(db => db.Set<BrowserContext>().Select(row => row.Generation).SingleAsync(TestContext.Current.CancellationToken)));
            Assert.Equal(ReplacedGeneration, await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().CountAsync(TestContext.Current.CancellationToken)));
            Assert.Equal(ReplacedGeneration, await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Start, TestContext.Current.CancellationToken)));
        }
        else
        {
            BrowserLoginTests.RequireDenied(response, HttpStatusCode.BadRequest);
            Assert.Equal(before, await scenario.SnapshotAsync());
            _ = await scenario.StartAsync(await scenario.EntryAsync());
        }
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.False(bypass, AntiforgeryMarker);
    }
}
