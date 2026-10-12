using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Exercises the genuine transaction-bound session capability without fabricating its constructor or authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityCapabilityTests(BootstrapFixture fixture)
{
    /// <summary>Detached or modified source rows cannot retain authority, and time checks retain captured immutable expiry.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_NativeSessionProof_When_TrackedSourceIsChangedOrDetached_Then_AuthorityAndExpiryCannotBeExtended(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        JourneySessionProof proof;
        await using (var transaction = await CeremonyTransaction.OpenAsync(scenario.Database, scenario.Get<NativeOperation>(),
            new(false, false, [], [grant.UserId]), token))
        {
            var account = await CeremonyAccount.LoadAsync(scenario.Database, transaction, grant.UserId);
            proof = (await JourneySessionProof.ReadAsync(scenario.Database, scenario.Get<UserManager<PlatformUser>>(),
                scenario.Get<CeremonyClock>(), transaction, account, SecurityScenario.Candidate(grant)))!;
            Assert.NotNull(proof);
            var now = scenario.Clock.Now.ToUnixTimeSeconds();
            proof.RequireCurrent(transaction, account, now);
            var session = await scenario.Database.Set<AdministratorSession>().SingleAsync(token);
            session.ExpiresAt = long.MaxValue;
            Assert.Throws<FactorUnavailableException>(() => proof.RequireCurrent(transaction, account, now));
            Assert.Throws<FactorUnavailableException>(() => proof.RequireTime(grant.ExpiresAt));
            session.ExpiresAt = grant.ExpiresAt;
            scenario.Database.Entry(session).State = EntityState.Unchanged;
            proof.RequireCurrent(transaction, account, now);
            scenario.Database.Entry(session).State = EntityState.Detached;
            Assert.Throws<FactorUnavailableException>(() => proof.RequireCurrent(transaction, account, now));
        }
        await using var other = await CeremonyTransaction.OpenAsync(scenario.Database, scenario.Get<NativeOperation>(),
            new(false, false, [], [grant.UserId]), token);
        var current = await CeremonyAccount.LoadAsync(scenario.Database, other, grant.UserId);
        Assert.Throws<FactorUnavailableException>(() => proof.RequireCurrent(other, current, scenario.Clock.Now.ToUnixTimeSeconds()));
    }
}
