using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Proves unsupported paths and changed internal bindings cannot become factor authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class DenialTests(BootstrapFixture fixture)
{
    private const string DetachedChange = "detached";
    private const string DeletedChange = "deleted";
    private const int RecoveryCount = 10;
    private const int UnsupportedCodeCount = 9;

    private const string PostgreSqlProvider = "PostgreSql";
    private const string SqlServerProvider = "SqlServer";
    private const string Owner = "owner";
    private const string GenerationChange = "generation";
    private const string StampChange = "stamp";
    private const string PurposeChange = "purpose";
    private const string BrowserChange = "browser";
    private const string ExpiryChange = "expiry";
    private const string StagedChange = "staged";
    private const string TransactionChange = "transaction";
    private const string ProviderTrait = "Provider";
    private const string SyntheticMaterial = "synthetic";
    private const string AuthenticatorToken = "AuthenticatorKey";
    private const string RecoveryToken = "RecoveryCodes";
    private const string Padding = " ";
    private const string ChangedSuffix = "changed";
    private const string ReplacementPurpose = "Replacement";
    private const string UnknownMutationMessage = "Unknown binding mutation.";
    private const string ActiveState = "Active";
    private const string CorruptMaterial = "corrupt";

    private static readonly string[] Scenarios0 = new[] { PostgreSqlProvider, SqlServerProvider };
    private static readonly string[] Scenarios1 = new[] { "[AspNetUserStore]", "[AspNetUserStore] ", "[aspnetuserstore]", "［AspNetUserStore］", "", "unsafe provider" };
    private static readonly string[] Scenarios2 = new[] { Owner, GenerationChange, StampChange, PurposeChange, BrowserChange, ExpiryChange, StagedChange, TransactionChange, DetachedChange, DeletedChange };
    /// <summary>Enumerates provider-collation aliases without including secrets in test identities.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> ReservedProviders =>
        from provider in Scenarios0
        from alias in Scenarios1
        select new TheoryDataRow<string, string>(provider, alias).WithTrait(ProviderTrait, provider);

    /// <summary>Exercises direct native store and manager generic-token routes, including SQL padded and Unicode aliases.</summary>
    [Theory, MemberData(nameof(ReservedProviders))]
    public async Task Given_ReservedProviderAlias_When_GenericTokenApisRun_Then_PlaintextFallbackIsDenied(string provider, string alias)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await using var scope = lease.Runtime.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var store = Assert.IsType<PlatformIdentityStore>(scope.ServiceProvider.GetRequiredService<IUserStore<PlatformUser>>());
        var user = new PlatformUser();
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.SetAuthenticationTokenAsync(user, alias, AuthenticatorToken, SyntheticMaterial));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.GetAuthenticationTokenAsync(user, alias, AuthenticatorToken));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.RemoveAuthenticationTokenAsync(user, alias, RecoveryToken));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => store.SetTokenAsync(user, alias, RecoveryToken, SyntheticMaterial, TestContext.Current.CancellationToken));
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.UserTokens.ToListAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Proves the Migrator preserves native password behavior without factor access or Data Protection resolution.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PasswordOnlyHost_When_NativeFactorApisRun_Then_EveryOperationIsUnavailable(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await lease.InitializeAsync(TestContext.Current.CancellationToken);
        await using var scope = lease.Runtime.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var store = Assert.IsType<PlatformIdentityStore>(scope.ServiceProvider.GetRequiredService<IUserStore<PlatformUser>>());
        var user = (await users.FindByNameAsync(lease.Username))!;
        Assert.True(await users.CheckPasswordAsync(user, lease.Password));
        Assert.Null(scope.ServiceProvider.GetService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.GetAuthenticatorKeyAsync(user));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.ResetAuthenticatorKeyAsync(user));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCount));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.CountRecoveryCodesAsync(user));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => users.RedeemTwoFactorRecoveryCodeAsync(user, SyntheticMaterial));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => store.SetAuthenticatorKeyAsync(user, SyntheticMaterial, TestContext.Current.CancellationToken));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().UserTokens.ToListAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Enumerates mutations to immutable selections and proofs independently from production predicate branches.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> BindingChanges =>
        from provider in Scenarios0
        from change in Scenarios2
        select new TheoryDataRow<string, string>(provider, change).WithTrait(ProviderTrait, provider);

    /// <summary>Rejects every post-verification persisted-binding change before installation can mutate native state.</summary>
    [Theory, MemberData(nameof(BindingChanges))]
    public async Task Given_VerifiedStaging_When_BindingChanges_Then_InstallationIsDenied(string provider, string change)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        var proof = await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System,
            interaction.Id, FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), state.Token);
        Assert.NotNull(proof);
        await ChangeBindingAsync(state.Database, interaction, transaction, change, state.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => Assert.IsType<AdministratorUserManager>(state.Users).InstallAsync(proof, state.Token));
        Assert.Empty(await state.Database.Set<AdministratorFactor>().AsNoTracking().ToArrayAsync(state.Token));
    }

    /// <summary>Prevents mutable selected entities, nested leases and detached request objects from retargeting native factor operations.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SelectedMaterial_When_TrackedBindingsMutate_Then_NativeAccessIsDeniedAndLeaseClears(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        await state.SelectRecoveryGenerationAsync();
        Assert.Throws<FactorUnavailableException>(() => state.Access.Select(interaction, state.Token));
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        Assert.Throws<FactorUnavailableException>(() => state.Access.Select(new AdministratorInteraction(), state.Token));
        using (state.Access.Select(interaction, state.Token))
        {
            Assert.Throws<FactorUnavailableException>(() => state.Access.SelectRecovery(state.Enrollment, state.Token));
            interaction.DestinationGeneration++;
            await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GetAuthenticatorKeyAsync(state.User));
        }
        using (state.Access.SelectRecovery(state.Enrollment, state.Token))
        {
            state.Enrollment.RecoveryGeneration++;
            await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.CountRecoveryCodesAsync(state.User));
        }
        Assert.Null(state.Access.Staged);
        Assert.Null(state.Access.Recovery);
        Assert.Equal(CancellationToken.None, state.Access.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.ResetAuthenticatorKeyAsync(state.User));
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GenerateNewTwoFactorRecoveryCodesAsync(state.User, UnsupportedCodeCount));
    }

    /// <summary>Ensures active seed reads do not accept SQL-padded owner aliases or missing, suspended and corrupt material.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ActiveMaterial_When_OwnerOrAvailabilityChanges_Then_UnusableMaterialFailsClosed(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GetAuthenticatorKeyAsync(state.User));
        state.Enrollment.State = ActiveState;
        state.Enrollment.FactorGeneration = 1;
        await state.Database.SaveChangesAsync(state.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GetAuthenticatorKeyAsync(state.User));
        var factor = new AdministratorFactor { UserId = state.User.Id, ProtectedSeed = state.Protection.Protect(FactorScenario.Seed, state.User.Id, 1, FactorProtection.Active) };
        state.Database.Add(factor);
        await state.Database.SaveChangesAsync(state.Token);
        Assert.True(await state.Users.GetAuthenticatorKeyAsync(state.User) == FactorScenario.Seed);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GetAuthenticatorKeyAsync(new PlatformUser { Id = state.User.Id + Padding }));
        state.Enrollment.Suspended = true;
        await state.Database.SaveChangesAsync(state.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GetAuthenticatorKeyAsync(state.User));
        state.Enrollment.Suspended = false;
        factor.ProtectedSeed = CorruptMaterial;
        await state.Database.SaveChangesAsync(state.Token);
        var error = await Assert.ThrowsAsync<FactorUnavailableException>(() => state.Users.GetAuthenticatorKeyAsync(state.User));
        Assert.Null(error.InnerException);
    }

    /// <summary>Mutates exactly one proof binding without persisting it or changing the independent expected rejection.</summary>
    private static async Task ChangeBindingAsync(PlatformDbContext database, AdministratorInteraction interaction,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, string change, CancellationToken token)
    {
        switch (change)
        {
            case Owner:
                interaction.UserId += Padding;
                break;
            case GenerationChange:
                interaction.DestinationGeneration++;
                break;
            case StampChange:
                interaction.SecurityStamp += ChangedSuffix;
                break;
            case PurposeChange:
                interaction.Purpose = ReplacementPurpose;
                break;
            case BrowserChange:
                interaction.BrowserDigest += ChangedSuffix;
                break;
            case ExpiryChange:
                interaction.ExpiresAt++;
                break;
            case StagedChange:
                interaction.StagedSeed += ChangedSuffix;
                break;
            case DetachedChange:
                database.Entry(interaction).State = EntityState.Detached;
                break;
            case DeletedChange:
                database.Entry(interaction).State = EntityState.Deleted;
                break;
            case TransactionChange:
                await transaction.RollbackAsync(token);
                break;
            default:
                throw new InvalidOperationException(UnknownMutationMessage);
        }
    }
}
