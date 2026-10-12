using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Proves a committed recovery transition cannot be resumed with the lost response's predecessor binding or spent code.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryLostResponseTests(BootstrapFixture fixture)
{
    private const string RecoveryEvent = "administrator-recovery";

    /// <summary>A client receiving no HTTP bytes must supply a fresh password and another code; the committed claim cannot be replayed or undone.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_UnreceivedRecoveryTransition_When_CommitSurvivesTransportLoss_Then_AnotherFreshPasswordAndCodeAreRequired(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        var location = await AdministratorJourney.BeginAtAsync(environment, browser, SecurityHttp.Recovery);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: false);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        string committed;
        await using (var transport = new UnreceivedResponse())
        {
            await transport.SendAsync(environment.Issuer, cookies, form, enrolled.RecoveryCodes[0], SecurityHttp.RecoveryVerify);
            await SecurityCommit.ObserveAsync(database, RecoveryEvent);
            committed = await SecurityCommit.RecoveryDigestAsync(database);
        }
        using var other = environment.Browser(cookies, secondary: true);
        using var replay = await AdministratorJourney.ConfirmAtAsync(other, form, enrolled.RecoveryCodes[0], SecurityHttp.RecoveryVerify);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(committed, await SecurityCommit.RecoveryDigestAsync(database));
        using var denied = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(1, await database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
        var next = await AdministratorJourney.BeginAtAsync(environment, other, SecurityHttp.Recovery);
        var challenge = await AdministratorJourney.ReadAsync(other, next, enrollment: false);
        using var spent = await AdministratorJourney.ConfirmAtAsync(other, challenge, enrolled.RecoveryCodes[0], SecurityHttp.RecoveryVerify);
        Assert.Equal(HttpStatusCode.BadRequest, spent.StatusCode);
        var replacement = await SecurityHttp.TransitionAsync(other, challenge, enrolled.RecoveryCodes[1], recovery: true);
        Assert.NotNull((await AdministratorJourney.ReadAsync(other, replacement, enrollment: true)).Seed);
    }
}
