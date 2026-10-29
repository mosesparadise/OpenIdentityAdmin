using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves API read ownership cannot be substituted for existing ceremony mutation or foreign native-context authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationReadOwnershipTests(BootstrapFixture fixture)
{
    private const string CommitBypass = "application-read-commit-bypass";
    private const string Callback = "https://administrator.example.test/callback";

    /// <summary>Every production commit entry rejects API mode, and even staged native database changes and lock increments roll back.</summary>
    [Trait("CriticalDecision", "application-read-commit")]
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ApiReadMode_When_AnyCommitPathIsAttempted_Then_AllWritesRemainUncommitted(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var services = Services(scenario, provider);
        var user = scenario.Account.ActorId;
        var revision = await scenario.Account.Journey.Database.Set<AdministratorProtocolProfile>().AsNoTracking().Select(row => row.LockRevision).SingleAsync(TestContext.Current.CancellationToken);
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await using var transaction = await OpenAsync(scope.ServiceProvider, user);
            var clock = new CeremonyClock(scenario.Account.Journey.Clock);
            await scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, user);
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, transaction.Token));
            await database.Users.Where(row => row.Id == user).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), transaction.Token);
            await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitAsync(CeremonyEvent.Password, true, user, clock.Now));
            await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitLogoutAsync(user, clock.Now, null!, clock));
            var denied = false;
            try
            {
                await transaction.CommitMaintenanceAsync();
            }
            catch (FactorUnavailableException) { denied = true; }
            Assert.True(denied, CommitBypass);
            await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitContinuationAsync(new IssuerContinuation(), clock));
            await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitGovernanceAsync(null!, clock));
            await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitGovernanceDenialAsync(null!, clock));
        }
        await using var fresh = services.CreateAsyncScope();
        var current = fresh.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await current.Users.Where(row => row.Id == user).Select(row => row.IsEnabled).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(revision, await current.Set<AdministratorProtocolProfile>().Select(row => row.LockRevision).SingleAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A valid foreign transaction cannot select ordinary native stores in another scope, even when it has the same provider and account.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ForeignApiTransaction_When_StoreSelectionIsAttempted_Then_ScopePermanentlyRejectsResolution(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var services = Services(scenario, provider);
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var database = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await OpenAsync(first.ServiceProvider, scenario.Account.ActorId);
        var selection = second.ServiceProvider.GetRequiredService<ProtocolStorageSelection>();
        await Assert.ThrowsAnyAsync<Exception>(() => selection.SelectApiReadAsync(transaction, database, scenario.Account.ActorId).AsTask());
        Assert.ThrowsAny<Exception>(() => second.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>());
        Assert.ThrowsAny<Exception>(() => selection.Select(ProtocolStorageMode.LegacyPilot));
    }

    /// <summary>Selection grants no reusable capability after the originating transaction has disposed.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DisposedApiTransaction_When_NativeStoreFirstResolves_Then_ExpiredScopeIsRejected(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var services = Services(scenario, provider);
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using (var transaction = await OpenAsync(scope.ServiceProvider, scenario.Account.ActorId))
        {
            await scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, scenario.Account.ActorId);
        }
        Assert.ThrowsAny<Exception>(() => scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>());
    }

    /// <summary>Creates an independent API service root with the real runtime provider and no issuer private credentials.</summary>
    private static ServiceProvider Services(AdministratorProtocolScenario scenario, string provider) => new ServiceCollection()
        .AddLogging(logging => logging.ClearProviders())
        .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), scenario.Account.Journey.Lease.RuntimeConnection)
        .AddSingleton<TimeProvider>(scenario.Account.Journey.Clock).AddApplicationReads(scenario.Issuer, Callback)
        .BuildServiceProvider(validateScopes: true);

    /// <summary>Acquires the real rollback-only transaction in the selected scoped context.</summary>
    private static Task<CeremonyTransaction> OpenAsync(IServiceProvider services, string user) => CeremonyTransaction.OpenApiReadAsync(
        services.GetRequiredService<PlatformDbContext>(), services.GetRequiredService<NativeOperation>(), user, TestContext.Current.CancellationToken);
}
