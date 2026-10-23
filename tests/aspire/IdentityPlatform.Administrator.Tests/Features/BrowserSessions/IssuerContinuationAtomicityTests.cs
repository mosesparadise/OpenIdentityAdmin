using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Reuses actual database permission failures to prove each new continuation write shares its owner's audit commit.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationAtomicityTests(BootstrapFixture fixture)
{
    private const string PostgresPermission = "42501";
    private const int SqlPermission = 229;

    /// <summary>Anonymous admission cannot retain a continuation when its safe start audit fails.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DeniedAuditInsert_When_ContinuationStarts_Then_NoRowOrBudgetMutationCommits(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var attempt = IssuerContinuationProviderFixture.Create();
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        await RequirePermissionFailureAsync(scenario, async () =>
            Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt)));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
    }

    /// <summary>Password binding, interaction admission and audit roll back together instead of retaining a partial source reference.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DeniedAuditInsert_When_PasswordBindsContinuation_Then_TheOriginalPendingIntentRemainsUnbound(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        await RequirePermissionFailureAsync(scenario, async () =>
            Assert.NotNull((await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt)).Interaction));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        Assert.Null((await IssuerContinuationProviderFixture.RowAsync(scenario, attempt)).InteractionId);
        Assert.NotNull((await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt)).Interaction);
    }

    /// <summary>Failed MFA audit releases no grant and spends neither the native factor nor the bound interaction/completion.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DeniedAuditInsert_When_NativeMfaCreatesCompletion_Then_AllAuthorityRollsBackAndTheOriginalFactorRemainsUsable(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var previous = await scenario.EnrollAsync();
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var login = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt);
        Assert.NotNull(login.Interaction);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        JourneyCompletion.Grant? grant = null;
        await RequirePermissionFailureAsync(scenario, async () => grant =
            await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code));
        Assert.Null(grant);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        grant = await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code);
        Assert.NotNull(grant);
        await IssuerContinuationProviderFixture.RequireGrantAsync(scenario, attempt, login.Interaction, previous, grant);
    }

    /// <summary>Denies only INSERT with existing fixture privileges, requires the actual provider error, then restores permission before further observations.</summary>
    private static async Task RequirePermissionFailureAsync(JourneyScenario scenario, Func<Task> operation)
    {
        await IssuerAuditPermission.RunAsync(scenario.Lease, async () =>
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(operation);
            Assert.True(failure.InnerException is Npgsql.PostgresException { SqlState: PostgresPermission } ||
                failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlPermission });
        });
    }
}
