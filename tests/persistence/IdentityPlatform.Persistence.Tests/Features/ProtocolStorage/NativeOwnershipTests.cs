using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Runs identical native-manager ownership assertions on both shipped providers and both protocol stores.</summary>
public sealed class NativeOwnershipTests
{
    private const string PostgresImage = "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string Postgres = "PostgreSql";
    private const string DatabaseName = "native_ownership";
    private const int DeadlineMinutes = 3;

    /// <summary>Uses actual installed managers, explicit transactions and least-privilege native connections in every cell.</summary>
    [Theory]
    [InlineData("PostgreSql", DatabaseStore.Platform, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", DatabaseStore.Sessions, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", DatabaseStore.Platform, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", DatabaseStore.Sessions, Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeManagers_When_OwnedTransactionsFinish_Then_CommitRollbackAndCancellationPreserveState(string name, DatabaseStore store)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        await using var postgres = new PostgreSqlBuilder(PostgresImage).Build();
        await using var sql = new MsSqlBuilder(SqlImage).Build();
        var connection = name == Postgres ? await StartPostgresAsync(postgres, deadline.Token) : await StartSqlAsync(sql, deadline.Token);
        var fixture = NativeProviderFixtures.Create(name, connection);
        await using (var migrator = new ServiceCollection().AddLogging().AddPlatformPersistence(fixture.Provider, connection, connection)
            .BuildServiceProvider(validateScopes: true))
        {
            Assert.Equal(0, await DatabaseUpgrade.RunAsync(migrator, deadline.Token));
        }
        await fixture.PrepareRuntimePrincipals(deadline.Token);
        using var material = new NativeAssertionMaterial();
        await using var root = CreateRoot(fixture, store, material);
        await using (var owned = await NativeOwnershipConformance.OpenAsync(root, deadline.Token))
        {
            var expected = store == DatabaseStore.Platform ? (DbContext)owned.Services.GetRequiredService<PlatformDbContext>() : owned.Services.GetRequiredService<AdminSessionDbContext>();
            Assert.Same(expected, owned.Owner.Database);
            Assert.Same(expected, await owned.Services.GetRequiredService<OpenIddict.EntityFrameworkCore.IOpenIddictEntityFrameworkCoreContext>().GetDbContextAsync(deadline.Token));
        }
        await NativeOwnershipConformance.VerifyAsync(root, deadline.Token);
        await NativeRequestConformance.VerifyAsync(root, deadline.Token);
        await NativeConsumptionConformance.VerifyAsync(root, store, deadline.Token);
        await NativeConsumptionBindings.VerifyAsync(root, store, deadline.Token);
        await NativeConsumptionRows.VerifyAsync(root, store, deadline.Token);
        await using var independent = CreateRoot(fixture, store, material);
        await NativeConsumptionRaces.VerifyAsync(root, independent, store, new NativeLockObservation(name, connection), deadline.Token);
        if (store == DatabaseStore.Platform)
        {
            await NativeConsumptionReceipts.VerifyAsync(root, independent, deadline.Token);
            await NativeAssertionAuthentication.VerifyAsync(fixture, material, deadline.Token);
            await NativeAssertionLedger.VerifyAsync(root, independent, deadline.Token);
        }
        await NativeConsumptionBoundaries.VerifyAsync(root, store, deadline.Token);
    }

    /// <summary>Selects native context composition explicitly; production registration remains unchanged.</summary>
    internal static ServiceProvider CreateRoot(NativeProviderFixture fixture, DatabaseStore store, NativeAssertionMaterial material)
    {
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(fixture.Provider,
            fixture.RuntimeConnection(DatabaseStore.Platform), fixture.RuntimeConnection(DatabaseStore.Sessions));
        if (store == DatabaseStore.Sessions)
        {
            services.AddOpenIddict().AddCore(core => core.UseEntityFrameworkCore().UseDbContext<AdminSessionDbContext>());
        }
        services.AddSingleton<CancelNativeUpdate>();
        services.AddSingleton<NativeConsumptionClock>();
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<NativeConsumptionClock>());
        services.AddSingleton<NativeConsumptionCommit>();
        services.AddSingleton<NativeRegistrationAuditFailure>();
        services.AddSingleton<NativeRedemptionBarrier>();
        services.AddDbContext<PlatformDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<CancelNativeUpdate>(),
            provider.GetRequiredService<NativeConsumptionCommit>(), provider.GetRequiredService<NativeRedemptionBarrier>(),
            provider.GetRequiredService<NativeRegistrationAuditFailure>()));
        services.AddDbContext<AdminSessionDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<CancelNativeUpdate>(),
            provider.GetRequiredService<NativeConsumptionCommit>(), provider.GetRequiredService<NativeRedemptionBarrier>()));
        services.AddProtocolStorageOwnership();
        services.AddSingleton(material.Policy);
        NativeConsumptionSetup.Configure(services, store);
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Starts only the selected PostgreSQL fixture.</summary>
    private static async Task<string> StartPostgresAsync(PostgreSqlContainer container, CancellationToken token)
    {
        await container.StartAsync(token);
        return container.GetConnectionString();
    }

    /// <summary>Starts only the selected SQL Server fixture with a disposable migration target.</summary>
    private static async Task<string> StartSqlAsync(MsSqlContainer container, CancellationToken token)
    {
        await container.StartAsync(token);
        return new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = DatabaseName }.ConnectionString;
    }
}
