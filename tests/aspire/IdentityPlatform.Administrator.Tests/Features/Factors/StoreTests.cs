using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Exercises native Identity dispatch against real runtime-role databases with no session authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class StoreTests(BootstrapFixture fixture)
{
    private const int RecoveryCodeCount = 10;
    private const int RecoveryCodeLength = 32;
    private const int DigestLength = 64;

    private const string PostgreSqlProvider = "PostgreSql";
    private const string ProviderTrait = "Provider";
    private const string SqlServerProvider = "SqlServer";
    private const string PendingState = "Pending";

    /// <summary>Runs the same native-store scenarios on both supported production providers.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => new TheoryDataRow<string>[]
    {
        new TheoryDataRow<string>(PostgreSqlProvider).WithTrait(ProviderTrait, PostgreSqlProvider),
        new TheoryDataRow<string>(SqlServerProvider).WithTrait(ProviderTrait, SqlServerProvider)
    };

    /// <summary>Proves real native OTP verification installs exactly the staged seed and rotates the stamp without activation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_PersistedStaging_When_NativeOtpIsVerified_Then_OnlyVerifiedSeedIsInstalled(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        var oldStamp = state.User.SecurityStamp;
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        var code = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var proof = await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users,
            TimeProvider.System, interaction.Id, code, state.Token);
        Assert.NotNull(proof);
        await Assert.IsType<AdministratorUserManager>(state.Users).InstallAsync(proof, state.Token);
        var factor = await state.Database.Set<AdministratorFactor>().SingleAsync(state.Token);
        Assert.True(state.Protection.Unprotect(factor.ProtectedSeed, state.User.Id, 1, FactorProtection.Active) == FactorScenario.Seed);
        Assert.True(factor.ProtectedSeed != FactorScenario.Seed && state.User.SecurityStamp != oldStamp);
        Assert.True(state.User.TwoFactorEnabled && !state.User.IsEnabled && state.Enrollment.State == PendingState);
        Assert.Empty(await state.Database.UserTokens.ToListAsync(state.Token));
        Assert.Empty(await state.Database.Set<AdministratorSession>().ToListAsync(state.Token));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => Assert.IsType<AdministratorUserManager>(state.Users).InstallAsync(proof, state.Token));
        await transaction.CommitAsync(state.Token);
    }

    /// <summary>Confirms cancellation and rollback never leave installed material or native MFA state.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_VerifiedSeed_When_InstallationRollsBack_Then_NativeStateAndMaterialAreUnchanged(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        var stamp = state.User.SecurityStamp;
        await using (var transaction = await state.Database.Database.BeginTransactionAsync(state.Token))
        {
            var proof = await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System,
                interaction.Id, FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), state.Token);
            Assert.NotNull(proof);
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Assert.IsType<AdministratorUserManager>(state.Users).InstallAsync(proof, cancelled.Token));
            await Assert.IsType<AdministratorUserManager>(state.Users).InstallAsync(proof, state.Token);
            await transaction.RollbackAsync(state.Token);
        }
        state.Database.ChangeTracker.Clear();
        var user = await state.Users.FindByIdAsync(state.User.Id);
        Assert.True(user is { TwoFactorEnabled: false, IsEnabled: false } && user.SecurityStamp == stamp);
        Assert.Empty(await state.Database.Set<AdministratorFactor>().ToListAsync(state.Token));
    }

    /// <summary>Proves native generation stores independent owner/generation-bound verifiers and claims each code once.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RecoveryGeneration_When_NativeCodesAreGeneratedAndRedeemed_Then_OnlyVerifiersPersistOnce(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await state.SelectRecoveryGenerationAsync();
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
        var codes = (await state.Users.GenerateNewTwoFactorRecoveryCodesAsync(state.User, RecoveryCodeCount))!.ToArray();
        Assert.Equal(10, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.True(codes.All(code => code.Length == RecoveryCodeLength && code.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F')));
        var rows = await state.Database.Set<AdministratorRecoveryCode>().ToArrayAsync(state.Token);
        Assert.True(rows.Select(row => row.Verifier).Order().SequenceEqual(codes.Select(code => FactorOracle.Verifier(state.User.Id, 1, code)).Order()));
        Assert.Empty(await state.Database.UserTokens.ToListAsync(state.Token));
        Assert.Equal(10, await state.Users.CountRecoveryCodesAsync(state.User));
        Assert.True((await state.Users.RedeemTwoFactorRecoveryCodeAsync(state.User, codes[0])).Succeeded);
        Assert.False((await state.Users.RedeemTwoFactorRecoveryCodeAsync(state.User, codes[0])).Succeeded);
        Assert.False((await state.Users.RedeemTwoFactorRecoveryCodeAsync(state.User, 'a' + codes[1][1..])).Succeeded);
        Assert.Equal(9, await state.Users.CountRecoveryCodesAsync(state.User));
        Assert.All(rows, row => Assert.Equal(EntityState.Detached, state.Database.Entry(row).State));
        await transaction.CommitAsync(state.Token);
    }

    /// <summary>Rejects pending tracker changes before bulk replacement and does not silently accept an eleventh verifier.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_PendingRecoveryRows_When_NativeReplacementRuns_Then_UnsafeTrackerStateIsRejected(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await state.SelectRecoveryGenerationAsync();
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        using var selection = state.Access.SelectRecovery(state.Enrollment, state.Token);
        state.Database.Add(new AdministratorRecoveryCode { UserId = state.User.Id, Verifier = new string('A', DigestLength) });
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GenerateNewTwoFactorRecoveryCodesAsync(state.User, RecoveryCodeCount));
        Assert.Empty(await state.Database.Set<AdministratorRecoveryCode>().AsNoTracking().ToArrayAsync(state.Token));
        await transaction.RollbackAsync(state.Token);
    }
}
