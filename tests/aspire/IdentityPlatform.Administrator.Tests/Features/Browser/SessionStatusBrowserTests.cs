using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves actual browser status reads preserve durable state and suppress identity after original expiry or real scope failure.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class SessionStatusBrowserTests(BootstrapFixture fixture)
{
    private const string StatusPath = "/bff/session";
    private const string Authenticated = "authenticated";
    private const string ExpiresAt = "expiresAt";
    private const string SetCookie = "set-cookie";
    private const string Location = "location";
    private const string Cache = "cache-control";
    private const string NoStore = "no-store";
    private const string CleanupFailure = "Synthetic browser status cleanup failure.";
    private const int AdvanceSeconds = 10;

    /// <summary>Real navigations and reload retain original ticket expiry and all Sessions rows, then equality at expiry denies without a new challenge.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealAuthenticatedBrowser_When_StatusReloadsAndOriginalExpiryArrives_Then_NoRenewalOrNewLoginOccurs(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var expiry = await ExpiryAsync(environment);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            await RequireAsync(await page.ReloadAsync(), true, expiry, environment.Issuer.Account.ActorId);
            environment.Issuer.Account.Journey.Clock.Now = environment.Issuer.Account.Journey.Clock.Now.AddSeconds(AdvanceSeconds);
            await RequireAsync(await page.ReloadAsync(), true, expiry, environment.Issuer.Account.ActorId);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            environment.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(expiry, TimeSpan.Zero);
            await RequireAsync(await page.ReloadAsync(), false, expiry, environment.Issuer.Account.ActorId);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            environment.RequirePolicies(1, 0, 0);
        });

    /// <summary>Actual read and post-context-disposal failures publish no identity; restored independent reads retain the same valid ticket without mutation.</summary>
    [Theory]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    public Task Given_RealAuthenticatedBrowser_When_ReadOrDisposalFails_Then_UnavailablePublishesNoIdentityAndRestoredReadSucceeds(string provider, bool read) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var probe = new BrowserStatusReadProbe(read, () => throw new InvalidOperationException(CleanupFailure));
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, probe.Configure);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var expiry = await ExpiryAsync(environment);
            probe.Armed = true;
            var denied = await page.GotoAsync(new Uri(environment.Profile.ClientUri, StatusPath).AbsoluteUri);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, denied!.Status);
            Assert.True(string.IsNullOrEmpty(await denied.TextAsync()));
            RequireHeaders(denied);
            Assert.Equal(1, probe.Calls);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await RequireAsync(await page.ReloadAsync(), true, expiry, environment.Issuer.Account.ActorId);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            Assert.Equal(1, environment.TokenRequests);
            environment.RequirePolicies(1, 0, 0);
        });

    /// <summary>Reads the original committed deadline independently from the actual provider.</summary>
    private static async Task<long> ExpiryAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<AdminSessionTicket>()
            .Select(row => row.IdleExpiresAt).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Checks public status and original expiry without printing identity or response content.</summary>
    private static async Task RequireAsync(IResponse? response, bool authenticated, long expiry, string subject)
    {
        Assert.NotNull(response);
        Assert.Equal(StatusCodes.Status200OK, response.Status);
        RequireHeaders(response);
        using var json = JsonDocument.Parse(await response.TextAsync());
        Assert.Equal(authenticated, json.RootElement.GetProperty(Authenticated).GetBoolean());
        Assert.Equal(authenticated ? Fields : [Authenticated], json.RootElement.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        if (authenticated)
        {
            Assert.True(json.RootElement.GetProperty(Subject).GetString() == subject);
            Assert.Equal(new DateTimeOffset(expiry, TimeSpan.Zero), json.RootElement.GetProperty(ExpiresAt).GetDateTimeOffset());
        }
    }

    /// <summary>Requires no cookie repair, redirect or caching on every status outcome.</summary>
    private static void RequireHeaders(IResponse response)
    {
        Assert.False(response.Headers.ContainsKey(SetCookie));
        Assert.False(response.Headers.ContainsKey(Location));
        Assert.True(response.Headers.TryGetValue(Cache, out var value) && value.Contains(NoStore, StringComparison.Ordinal));
    }
    private const string Subject = "subject";
    private static readonly string[] Fields = [Authenticated, ExpiresAt, Subject];
}
