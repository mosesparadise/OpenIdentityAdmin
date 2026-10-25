using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Distinguishes unavailable storage from logged-out authority and rechecks expiry after actual scope disposal.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserSessionFailureTests(BootstrapFixture fixture)
{
    private const string Unauthenticated = "{\"authenticated\":false}";
    private const string SetCookie = "Set-Cookie";
    private const string ProviderTrait = "Provider";
    private const string StatusPath = "/bff/session";
    private const string CleanupFailure = "Synthetic status cleanup failure.";
    private const string Read = "read";
    private const string Expiry = "expiry";
    private static readonly string[] Faults = [Read, Expiry, "cleanup"];

    /// <summary>No identity escapes failed reads/disposal; expiry at disposal returns unauthenticated and none of these reads mutates durable state.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ValidProductSession_When_ReadOrFinalCleanupFails_Then_NoIdentityOrRenewalEscapes(string provider, string fault)
    {
        BrowserLoginFixture? scenario = null;
        long expires = 0;
        var probe = new BrowserStatusReadProbe(fault == Read, () =>
        {
            if (fault == Expiry)
            {
                scenario!.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(expires, TimeSpan.Zero);
            }
            else
            {
                throw new InvalidOperationException(CleanupFailure);
            }
        });
        await using var owner = scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, probe.Configure);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        expires = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().Select(row => row.IdleExpiresAt).SingleAsync(TestContext.Current.CancellationToken));
        var before = await scenario.SnapshotAsync();
        probe.Armed = true;
        using var response = await scenario.Browser.GetAsync(StatusPath, TestContext.Current.CancellationToken);
        Assert.Equal(fault == Expiry ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(fault == Expiry ? Unauthenticated : string.Empty, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.Null(response.Headers.Location);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(before, await scenario.SnapshotAsync());
        using var recovered = await scenario.Browser.GetAsync(StatusPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(before, await scenario.SnapshotAsync());
        if (fault == Expiry)
        {
            Assert.Equal(Unauthenticated, await recovered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        else
        {
            var ticket = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
            await BrowserSessionStatusTests.RequireAuthenticatedAsync(scenario, ticket);
            Assert.Equal(before, await scenario.SnapshotAsync());
        }
    }

    /// <summary>Preserves provider identities for real database and scope cleanup cases.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Faults
        select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
