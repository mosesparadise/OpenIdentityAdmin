using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Checks recovery lifecycle primitives without treating code redemption as administrator authentication.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryTests(BootstrapFixture fixture)
{
    private const int CodeCount = 10;
    private const int CodeLength = 32;
    private const string MissingUser = "missing-user";
    private const string Malformed = "malformed";

    /// <summary>Replacement invalidates prior verifiers, rolls back atomically, and rejects mutation without scoped selection.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExistingCodes_When_ReplacementRollsBack_Then_PriorGenerationRemainsUsable(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await state.SelectRecoveryGenerationAsync();
        string[] original;
        await using (var transaction = await state.Database.Database.BeginTransactionAsync(state.Token))
        {
            using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
            original = (await state.Users.GenerateNewTwoFactorRecoveryCodesAsync(state.User, CodeCount))!.ToArray();
            await transaction.CommitAsync(state.Token);
        }
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.CountRecoveryCodesAsync(state.User));
        await using (var transaction = await state.Database.Database.BeginTransactionAsync(state.Token))
        {
            using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
            _ = await state.Users.GenerateNewTwoFactorRecoveryCodesAsync(state.User, CodeCount);
            Assert.False((await state.Users.RedeemTwoFactorRecoveryCodeAsync(state.User, original[0])).Succeeded);
            await transaction.RollbackAsync(state.Token);
        }
        state.Database.ChangeTracker.Clear();
        await using var final = await state.Database.Database.BeginTransactionAsync(state.Token);
        var enrollment = await state.Database.Set<AdministratorEnrollment>().SingleAsync(state.Token);
        using var restored = state.Access.SelectRecovery(enrollment, state.Token);
        Assert.Equal(10, await state.Users.CountRecoveryCodesAsync(state.User));
        Assert.True((await state.Users.RedeemTwoFactorRecoveryCodeAsync((await state.Users.FindByIdAsync(state.User.Id))!, original[0])).Succeeded);
        await final.CommitAsync(state.Token);
    }

    /// <summary>Observes two real connections contending on one verifier and proves exactly one committed conditional claim.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_OneRecoveryCode_When_TwoConnectionsContend_Then_ExactlyOneClaimCommits(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await state.SelectRecoveryGenerationAsync();
        var codes = await CreateCodesAsync(state);
        await using var otherScope = services.Services.CreateAsyncScope();
        var other = otherScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var otherAccess = otherScope.ServiceProvider.GetRequiredService<FactorAccess>();
        var otherStore = otherScope.ServiceProvider.GetRequiredService<ProtectedRecoveryStore>();
        var otherEnrollment = await other.Set<AdministratorEnrollment>().SingleAsync(state.Token);
        await using var first = await state.Database.Database.BeginTransactionAsync(state.Token);
        using var firstSelection = state.Access.SelectRecovery(state.Enrollment, state.Token);
        var store = new ProtectedRecoveryStore(state.Database, state.Access, TimeProvider.System);
        Assert.True(await store.RedeemAsync(state.User, codes[0], state.Token));
        var blocker = await DatabaseWaiters.IdentifyAsync(state.Database, state.Token);
        await using var second = await other.Database.BeginTransactionAsync(state.Token);
        using var secondSelection = otherAccess.SelectRecovery(otherEnrollment, state.Token);
        var contender = otherStore.RedeemAsync(state.User, codes[0], state.Token);
        const string RedemptionStage = "%UPDATE%AdministratorRecoveryCode%";
        await DatabaseWaiters.ObserveAsync(lease, blocker, RedemptionStage, 1);
        Assert.False(contender.IsCompleted);
        await first.CommitAsync(state.Token);
        Assert.False(await contender);
        await second.CommitAsync(state.Token);
        Assert.Equal(1, await other.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt != null, state.Token));
    }

    /// <summary>Creates committed native codes before a contention test opens its independent transactions.</summary>
    private static async Task<string[]> CreateCodesAsync(FactorScenario state)
    {
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
        var codes = (await state.Users.GenerateNewTwoFactorRecoveryCodesAsync(state.User, CodeCount))!.ToArray();
        await transaction.CommitAsync(state.Token);
        return codes;
    }

    /// <summary>Changing authoritative recovery generation cannot adopt verifiers from an older generation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_OldRecoveryVerifiers_When_GenerationChanges_Then_OldCodesCannotBeClaimed(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await state.SelectRecoveryGenerationAsync();
        var codes = await CreateCodesAsync(state);
        const int NextGeneration = 2;
        await state.SelectRecoveryGenerationAsync(NextGeneration);
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
        Assert.Equal(0, await state.Users.CountRecoveryCodesAsync(state.User));
        Assert.False((await state.Users.RedeemTwoFactorRecoveryCodeAsync(state.User, codes[0])).Succeeded);
        Assert.Equal(0, await state.Database.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt != null, state.Token));
    }

    /// <summary>Rejects unsupported input collections and ownership, leaving no verifiers after native update is denied.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ScopedRecovery_When_CodesOrOwnerAreInvalid_Then_StoreRejectsBeforeWriting(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await state.SelectRecoveryGenerationAsync();
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
        await using var scope = services.Services.CreateAsyncScope();
        var store = new ProtectedRecoveryStore(state.Database, state.Access, TimeProvider.System);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => store.ReplaceAsync(state.User, [], state.Token));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => store.ReplaceAsync(state.User, Enumerable.Repeat(new string('A', CodeLength), CodeCount), state.Token));
        var malformed = Enumerable.Range(0, CodeCount).Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        await Assert.ThrowsAsync<FactorUnavailableException>(() => store.ReplaceAsync(state.User, malformed, state.Token));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => store.CountAsync(new PlatformUser { Id = MissingUser }, state.Token));
        Assert.False(await store.RedeemAsync(state.User, Malformed, state.Token));
        Assert.Empty(await state.Database.Set<AdministratorRecoveryCode>().ToArrayAsync(state.Token));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReplaceAsync(state.User, [], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RedeemAsync(state.User, Malformed, cancelled.Token));
    }
}
