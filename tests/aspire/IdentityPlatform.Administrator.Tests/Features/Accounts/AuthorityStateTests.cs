using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Invalidates previously committed authority using independent persisted mutations and restored positive controls.</summary>
[Collection("administrator-bootstrap")]
public sealed class AuthorityStateTests(BootstrapFixture fixture)
{
    private const string CorruptMaterial = "synthetic-invalid-protected-material";
    private const string DifferentStamp = "synthetic-different-stamp";

    /// <summary>Revocation, stamp, generation and missing-row changes invalidate a genuine grant independently.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedSession_When_PersistedBindingsChange_Then_EachMismatchDeniesIndependently(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        await RequireSessionMutationAsync(scenario, grant, row => row.Revoked = true);
        await RequireSessionMutationAsync(scenario, grant, row => row.Generation++);
        await RequireSessionMutationAsync(scenario, grant, row => row.FactorGeneration++);
        await RequireSessionMutationAsync(scenario, grant, row => row.SecurityStamp = DifferentStamp);
        await RequireSessionMutationAsync(scenario, grant, row => row.PublicId = Infrastructure.Features.Identity.Ceremonies.CeremonySecrets.Create());
        var original = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        original.Generation++;
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        original = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        original.Generation--;
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }

    /// <summary>Unavailable protected material cannot be bypassed by a current cookie; restoring exactly that material restores validation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedSession_When_ProtectedFactorIsCorrupt_Then_NoAuthorityOrActivityIsReturned(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var factor = await scenario.Database.Set<AdministratorFactor>().SingleAsync(token);
        var original = factor.ProtectedSeed;
        factor.ProtectedSeed = CorruptMaterial;
        await scenario.Database.SaveChangesAsync(token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.Equal(0, (await scenario.Database.Set<AdministratorSession>().SingleAsync(token)).Revision);
        factor = await scenario.Database.Set<AdministratorFactor>().SingleAsync(token);
        factor.ProtectedSeed = original;
        await scenario.Database.SaveChangesAsync(token);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
    }

    /// <summary>Changes one persisted ticket binding and restores the snapshot before requiring a positive validation control.</summary>
    private static async Task RequireSessionMutationAsync(JourneyScenario scenario, JourneyCompletion.Grant grant, Action<AdministratorSession> mutate)
    {
        var token = TestContext.Current.CancellationToken;
        var session = await scenario.Database.Set<AdministratorSession>().SingleAsync(token);
        var snapshot = scenario.Database.Entry(session).CurrentValues.Clone();
        mutate(session);
        await scenario.Database.SaveChangesAsync(token);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        session = await scenario.Database.Set<AdministratorSession>().SingleAsync(token);
        scenario.Database.Entry(session).CurrentValues.SetValues(snapshot);
        await scenario.Database.SaveChangesAsync(token);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
    }
}
