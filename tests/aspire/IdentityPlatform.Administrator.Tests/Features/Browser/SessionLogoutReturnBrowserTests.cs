using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves original browser correlation, exact framing and expiry through actual HTTPS navigation and real stores.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutReturnBrowserTests(BootstrapFixture fixture)
{
    private const int SubstitutionAndReturnNavigations = 5;
    private const string State = "state";
    private const string Duplicate = "&state=";

    /// <summary>Foreign context, changed and duplicated state cannot consume the genuine return, which still conveys no issuer attestation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_GenuineLogoutCorrelation_When_BrowserOrStateIsSubstituted_Then_DenialPreservesOriginalOneUseReturn(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var source = await BrowserLogoutJourney.BeginAsync(environment, page);
            var destination = BrowserLogoutJourney.Return(environment, await BrowserLogoutJourney.LinkAsync(environment, page));
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            await using (var foreign = await BrowserSession.CreateAsync())
            {
                await BrowserLogoutJourney.DeniedAsync(await foreign.Context.NewPageAsync(), destination);
            }
            var forged = new Uri(QueryHelpers.AddQueryString(destination.GetLeftPart(UriPartial.Path), State, BrowserLoginSelector.CreateSelector()));
            await BrowserLogoutJourney.DeniedAsync(page, forged);
            await BrowserLogoutJourney.DeniedAsync(page, new Uri(destination.AbsoluteUri + Duplicate + QueryHelpers.ParseQuery(destination.Query)[State]));
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: true, remote: true);
            await page.GotoAsync(destination.AbsoluteUri);
            await BrowserLogoutJourney.RequireReturnedAsync(environment, page);
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.ReturnedUnverified);
            await BrowserLogoutJourney.DeniedAsync(page, destination);
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: true, remote: false);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            Assert.Equal(0, environment.EndSessionGets);
            Assert.Equal(0, environment.EndSessionPosts);
            environment.RequirePolicies(logins: 1, logouts: 1, confirmations: 0, returnReloads: SubstitutionAndReturnNavigations);
        });

    /// <summary>Application-clock expiry at the persisted original equality denies a real retained cookie without extending state or issuer authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_OriginalLogoutExpiry_When_ReturnArrivesAtEquality_Then_LocalRevocationPersistsAndCorrelationIsNotConsumed(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var source = await BrowserLogoutJourney.BeginAsync(environment, page);
            var destination = BrowserLogoutJourney.Return(environment, await BrowserLogoutJourney.LinkAsync(environment, page));
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            environment.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(await BrowserHttpsState.ExpiryAsync(environment), TimeSpan.Zero);
            await BrowserLogoutJourney.DeniedAsync(page, destination);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.RemotePending);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            Assert.Equal(0, environment.EndSessionGets);
            Assert.Equal(0, environment.EndSessionPosts);
            environment.RequirePolicies(logins: 1, logouts: 1, confirmations: 0, returnReloads: 1);
        });
}
