using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Requires native crypto and durable cleanup-proof ownership on both actual restricted provider connections.</summary>
public sealed class NativeCleanupAssertionTests
{
    private const string PostgresImage = "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string Postgres = "PostgreSql";
    private const string DatabaseName = "cleanup_assertion";
    private const string Operator = "cleanup-proof-test";
    private const int DeadlineMinutes = 3;

    /// <summary>Cross-instance replay and central disablement preserve exact native proof and registration boundaries.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_NativeCleanupProof_When_EligibilityAndReplayAreExercised_Then_OnlyExactDenyOnlyProofIsConsumed(string provider) =>
        RunAsync(provider, CleanupAssertionEligibility.VerifyAsync);

    /// <summary>Commit, disposal and fresh-epoch substitutions never restore a proof or permit foreign continuation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_ConsumedCleanupProof_When_ReceiptAndCommitBoundariesChange_Then_OnlyOriginalLiveOwnershipContinues(string provider) =>
        RunAsync(provider, CleanupAssertionOwnership.VerifyAsync);

    /// <summary>The disabled cleanup path retains the same finite replay quota, tombstones and monotonic expiry rules.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_DisabledCleanupProfile_When_LedgerBoundariesAreReached_Then_RetentionAndCapacityRemainStrict(string provider) =>
        RunAsync(provider, CleanupAssertionBounds.VerifyAsync);

    /// <summary>Owns one disposable database and two independent native roots, without extending any operation deadline.</summary>
    private static async Task RunAsync(string name, Func<CleanupAssertionFixture, CancellationToken, Task> verify)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        await using var postgres = new PostgreSqlBuilder(PostgresImage).Build();
        await using var sql = new MsSqlBuilder(SqlImage).Build();
        string connection;
        if (name == Postgres)
        {
            await postgres.StartAsync(deadline.Token);
            connection = postgres.GetConnectionString();
        }
        else
        {
            await sql.StartAsync(deadline.Token);
            connection = new SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = DatabaseName }.ConnectionString;
        }
        var provider = NativeProviderFixtures.Create(name, connection);
        await using (var migrator = new ServiceCollection().AddLogging().AddPlatformPersistence(provider.Provider, connection, connection)
            .BuildServiceProvider(validateScopes: true))
        {
            Assert.Equal(0, await DatabaseUpgrade.RunAsync(migrator, deadline.Token));
        }
        await provider.PrepareRuntimePrincipals(deadline.Token);
        using var material = new NativeAssertionMaterial(browser: true);
        await using var first = NativeAssertionAuthentication.CreateRoot(provider, material, cleanup: true);
        await using var second = NativeAssertionAuthentication.CreateRoot(provider, material, cleanup: true);
        Assert.True(await AdministratorRegistrationInitializer.RunAsync(first, material.Policy, Operator, Guid.NewGuid(), deadline.Token));
        await verify(new(first, second, material), deadline.Token);
    }
}
