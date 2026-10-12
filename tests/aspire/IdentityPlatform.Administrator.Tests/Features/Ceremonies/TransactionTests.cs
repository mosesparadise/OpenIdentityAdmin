using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Exercises actual database serialization, cancellation and immutable transaction-bound proof capabilities.</summary>
[Collection("administrator-bootstrap")]
public sealed class TransactionTests(BootstrapFixture fixture)
{
    private const string UserLockStage = "%UPDATE%AdministratorAuthentication%";
    private const int DrainSeconds = 40;
    private const string OtherUser = "unlocked-other-user";

    /// <summary>Two native code verifications visibly contend on the account lock and produce one accepted reservation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CompetingNativeProofs_When_AccountLockSerializes_Then_ExactlyOneReservationSucceeds(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var secondScenario = new CeremonyScenario(factors);
        await secondScenario.StageAsync();
        var code = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await using var first = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(first);
        var proof = await scenario.Get<CeremonyReplay>().VerifyAsync(first, account, (await scenario.ReadAsync(first, account))!, code, true);
        Assert.NotNull(proof);
        var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, scenario.Token);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(scenario.Token);
        var contender = CompeteAsync(services, secondScenario, code, stop.Token);
        try
        {
            await DatabaseWaiters.ObserveAsync(lease, blocker, UserLockStage, 1);
            Assert.False(contender.IsCompleted);
            await first.CommitAsync(CeremonyEvent.Factor, true, scenario.UserId, proof.VerifiedAt);
            Assert.False(await contender);
        }
        finally { await stop.CancelAsync(); await DrainAsync(contender); }
        Assert.Equal(1, await factors.Database.Set<AdministratorReplay>().CountAsync(scenario.Token));
    }

    /// <summary>Uses a genuinely independent native manager/context/connection for the competing verification.</summary>
    private static async Task<bool> CompeteAsync(FactorServices services, CeremonyScenario scenario, string code, CancellationToken token)
    {
        await using var scope = services.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var operation = scope.ServiceProvider.GetRequiredService<NativeOperation>();
        await using var transaction = await CeremonyTransaction.OpenAsync(database, operation,
            new(false, false, [scenario.Interaction.BrowserBudgetId], [scenario.UserId]), token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, scenario.UserId);
        var bound = await CeremonyInteraction.ReadAsync(database, transaction, account, scenario.Interaction.PublicId,
            scenario.Secret, scenario.Interaction.Purpose, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.NotNull(bound);
        var proof = await scope.ServiceProvider.GetRequiredService<CeremonyReplay>().VerifyAsync(transaction, account, bound, code, true);
        await transaction.CommitAsync(CeremonyEvent.Factor, proof is not null, scenario.UserId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return proof is not null;
    }

    /// <summary>Drains cancellation without masking the primary race assertion or leaking an active database operation.</summary>
    private static async Task DrainAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(DrainSeconds));
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Caller array mutation and cleanup commit cannot authorize unlocked state or bypass an ordinary ceremony audit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_OpenCeremony_When_CallerMutatesPlanOrBypassesAudit_Then_AuthorityIsRejected(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        string[] users = [factors.User.Id];
        var plan = new CeremonyLockPlan(false, false, [], users);
        users[0] = OtherUser;
        Assert.Equal(factors.User.Id, plan.Users[0]);
        await using var transaction = await CeremonyTransaction.OpenAsync(factors.Database,
            factors.Services.GetRequiredService<NativeOperation>(), plan, factors.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => CeremonyAccount.LoadAsync(factors.Database, transaction, OtherUser));
        await Assert.ThrowsAsync<FactorUnavailableException>(transaction.CommitMaintenanceAsync);
        Assert.Equal(1, await factors.Database.Set<SecurityAuditIntent>().CountAsync(factors.Token));
    }

    /// <summary>Cancellation reaches a waiting native provider operation and leaves the previously committed authentication state unchanged.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ContendedAccount_When_OperationIsCancelled_Then_NoPartialBudgetCommits(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await using var first = await scenario.OpenAsync();
        var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, scenario.Token);
        await using var scope = services.Services.CreateAsyncScope();
        var other = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(scenario.Token);
        var waiting = CeremonyTransaction.OpenAsync(other, scope.ServiceProvider.GetRequiredService<NativeOperation>(),
            new(false, false, [], [scenario.UserId]), stop.Token);
        try
        {
            await DatabaseWaiters.ObserveAsync(lease, blocker, UserLockStage, 1);
            var diagnostic = await CeremonyCancellation.CancelAndDrainAsync(waiting, stop, other.Database.IsSqlServer());
            Assert.True(diagnostic is null, diagnostic);
        }
        finally { _ = await CeremonyCancellation.CancelAndDrainAsync(waiting, stop, other.Database.IsSqlServer()); }
        Assert.Equal(0, (await factors.Database.Set<AdministratorAuthentication>().SingleAsync(factors.Token)).Failures);
    }
}
