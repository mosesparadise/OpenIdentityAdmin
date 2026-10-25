using System.Net;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves actual product authentication status is minimal, generation-bound and strictly read-only on both databases.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserSessionStatusTests(BootstrapFixture fixture)
{
    private const string Authenticated = "authenticated";
    private const string ExpiresAt = "expiresAt";
    private const string Subject = "subject";
    private const string JsonMedia = "application/json";
    private const string ProviderTrait = "Provider";
    private const string StatusPath = "/bff/session";
    private const string SetCookie = "Set-Cookie";
    private const int ReadAdvanceSeconds = 10;

    /// <summary>Two real status reads return the protected local identity and original effective expiry without touching any durable state or cookie.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_CommittedProductSession_When_StatusIsReadRepeatedly_Then_MinimalIdentityAndOriginalExpiryDoNotRenew(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var ticket = await scenario.ReadAsync(database => database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        var before = await scenario.SnapshotAsync();
        await RequireAuthenticatedAsync(scenario, ticket);
        scenario.Issuer.Account.Journey.Clock.Now = scenario.Issuer.Account.Journey.Clock.Now.AddSeconds(ReadAdvanceSeconds);
        await RequireAuthenticatedAsync(scenario, ticket);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>The response contains exactly the public fields and omits cookie, token, source-session and permission material.</summary>
    internal static async Task RequireAuthenticatedAsync(BrowserLoginFixture scenario, AdminSessionTicket ticket)
    {
        using var response = await scenario.Browser.GetAsync(StatusPath, TestContext.Current.CancellationToken);
        RequireSafe(response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal([Authenticated, ExpiresAt, Subject], json.RootElement.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        Assert.True(json.RootElement.GetProperty(Authenticated).GetBoolean());
        Assert.Equal(ticket.Subject, json.RootElement.GetProperty(Subject).GetString());
        Assert.Equal(new DateTimeOffset(ticket.IdleExpiresAt, TimeSpan.Zero), json.RootElement.GetProperty(ExpiresAt).GetDateTimeOffset());
    }

    /// <summary>Absent, expired or corrupt session material is not authority and never causes cookie repair or hidden renewal.</summary>
    [Theory, MemberData(nameof(Denials))]
    public async Task Given_InvalidProductAuthority_When_StatusIsRead_Then_OnlyUnauthenticatedStatusIsReturned(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        await DamageAsync(scenario, fault);
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.Browser.GetAsync(StatusPath, TestContext.Current.CancellationToken);
        RequireSafe(response);
        Assert.Equal("{\"authenticated\":false}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Applies one explicitly test-owned negative after genuine product creation, never fabricating positive authority.</summary>
    private static async Task DamageAsync(BrowserLoginFixture scenario, string fault)
    {
        if (fault == Missing)
        {
            scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Expired = true;
            return;
        }
        await scenario.ReadAsync(async database =>
        {
            var ticket = await database.Set<AdminSessionTicket>().SingleAsync(TestContext.Current.CancellationToken);
            if (fault == Expired)
            {
                scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(ticket.IdleExpiresAt, TimeSpan.Zero);
            }
            else if (fault == Revoked)
            {
                ticket.RevokedAt = scenario.Issuer.Account.Journey.Clock.Now.UtcTicks;
            }
            else
            {
                ticket.ProtectedTicket[0] ^= 1;
                database.Entry(ticket).Property(row => row.ProtectedTicket).IsModified = true;
            }
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
    }

    /// <summary>Requires a noncacheable JSON response with no authentication or navigation side effects.</summary>
    private static void RequireSafe(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(JsonMedia, response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.Null(response.Headers.Location);
    }

    private const string Missing = "missing-generation";
    private const string Expired = "expired";
    private const string Revoked = "revoked";
    private static readonly string[] Faults = [Missing, Expired, Revoked, "tampered"];

    /// <summary>Preserves exact provider discovery metadata for independent authority failures.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Denials =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Faults
        select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
