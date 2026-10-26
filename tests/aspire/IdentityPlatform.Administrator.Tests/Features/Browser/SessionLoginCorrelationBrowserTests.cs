using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Submits a genuine callback in an independent browser after its original transport has been suppressed and joined.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserLogin")]
public sealed class SessionLoginCorrelationBrowserTests(BootstrapFixture fixture)
{
    private const int TwoLogins = 2;
    private const int OtherReturns = 5;
    private static readonly BrowserHttpsCounts IssuerCounts = new(4, 2, 4, 6);

    /// <summary>Wrong browser correlation burns native state without exchange; original replay also denies, while an independent fresh sign-in succeeds.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_GenuineCallback_When_AnotherBrowserPresentsIt_Then_StateIsConsumedWithoutTicketOrExchange(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var original = await BrowserSession.CreateAsync();
            await using var foreign = await BrowserSession.CreateAsync();
            var page = await original.Context.NewPageAsync();
            await BrowserHttpsJourney.BeginAsync(environment, page);
            await BrowserHttpsJourney.PasswordAsync(environment, page);
            var callback = await BrowserLostCallback.CaptureAsync(environment, page);
            await RequireStateAsync(environment, redeemed: false);
            var other = await foreign.Context.NewPageAsync();
            var denied = await other.GotoAsync(callback.AbsoluteUri);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, denied!.Status);
            Assert.Equal(0, environment.TokenRequests);
            await BrowserLoginEffects.NoTicketAsync(environment);
            await RequireStateAsync(environment, redeemed: true);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var replay = await page.GotoAsync(callback.AbsoluteUri);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, replay!.Status);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            Assert.Equal(0, environment.TokenRequests);
            await BrowserHttpsJourney.SignInAsync(environment, other);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            Assert.Equal(TwoLogins, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            environment.RequirePolicies(new(TwoLogins, OtherReturns + environment.CallbackFailure.Count, ThreeForms, FiveRedirects), IssuerCounts);
        });

    /// <summary>Reads the original native state status independently; a rejection before native consumption is insufficient evidence.</summary>
    private static async Task RequireStateAsync(BrowserHttpsFixture fixture, bool redeemed)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(redeemed ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Valid, row.Status);
    }
    private const int ThreeForms = 3;
    private const int FiveRedirects = 5;
}
