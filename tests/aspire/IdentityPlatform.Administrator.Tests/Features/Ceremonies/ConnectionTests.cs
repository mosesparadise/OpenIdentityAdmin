using System.Data;
using System.Data.Common;
using System.Globalization;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Checks real session-setting restoration, nested ownership and failed-session discard for both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ConnectionTests(BootstrapFixture fixture)
{
    private const string SqlRead = "SELECT @@LOCK_TIMEOUT";
    private const string PostgresRead = "SELECT current_setting('lock_timeout')";
    private const string SqlIdentity = "SELECT @@SPID";
    private const string PostgresIdentity = "SELECT pg_backend_pid()";
    private const int OriginalCommandSeconds = 17;

    /// <summary>Nested cleanup and transaction connection owners restore the outer limits and then the caller's settings.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExternallyOpenedConnection_When_NestedOwnersLeave_Then_OriginalSessionAndCommandLimitsReturn(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        var database = factors.Database;
        await database.Database.OpenConnectionAsync(factors.Token);
        database.Database.SetCommandTimeout(OriginalCommandSeconds);
        var query = database.Database.IsSqlServer() ? SqlRead : PostgresRead;
        var original = await ScalarAsync(database, query);
        await using (var outer = new CeremonyConnection(database))
        {
            await outer.OpenAsync(factors.Token);
            var bounded = await ScalarAsync(database, query);
            Assert.Equal(database.Database.IsSqlServer() ? "5000" : "5s", bounded);
            await using (var inner = new CeremonyConnection(database))
            {
                await inner.OpenAsync(factors.Token);
            }
            Assert.Equal(bounded, await ScalarAsync(database, query));
            Assert.Equal(5, database.Database.GetCommandTimeout());
        }
        Assert.Equal(ConnectionState.Open, database.Database.GetDbConnection().State);
        Assert.Equal(original, await ScalarAsync(database, query));
        Assert.Equal(OriginalCommandSeconds, database.Database.GetCommandTimeout());
        await database.Database.CloseConnectionAsync();
    }

    /// <summary>A server-terminated session either fails restoration or is already closed; ownership never returns it as a live session.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_TerminatedSession_When_OwnershipEnds_Then_PhysicalConnectionIsDiscarded(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        var database = factors.Database;
        database.Database.SetCommandTimeout(OriginalCommandSeconds);
        await using var owner = new CeremonyConnection(database);
        await owner.OpenAsync(factors.Token);
        var session = int.Parse(await ScalarAsync(database, database.Database.IsSqlServer() ? SqlIdentity : PostgresIdentity), CultureInfo.InvariantCulture);
        await TerminateAsync(lease, session);
        var failure = await Record.ExceptionAsync(() => owner.DisposeAsync().AsTask());
        Assert.True(failure is null or DbException, failure?.GetType().Name);
        Assert.Equal(ConnectionState.Closed, database.Database.GetDbConnection().State);
        Assert.Equal(OriginalCommandSeconds, database.Database.GetCommandTimeout());
        await database.Database.OpenConnectionAsync(factors.Token);
        Assert.NotEmpty(await ScalarAsync(database, database.Database.IsSqlServer() ? SqlIdentity : PostgresIdentity));
        await database.Database.CloseConnectionAsync();
    }

    /// <summary>Reads only nonsecret setting/session metadata on the existing connection.</summary>
    private static async Task<string> ScalarAsync(PlatformDbContext database, string query)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }

    /// <summary>Uses fixture administration to terminate only the independently observed synthetic runtime session.</summary>
    private static async Task TerminateAsync(BootstrapDatabase lease, int session)
    {
        const string SqlTerminate = "KILL ";
        const string PostgresTerminate = "SELECT pg_terminate_backend(";
        const string CloseArgument = ")";
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        var identity = session.ToString(CultureInfo.InvariantCulture);
        command.CommandText = database.Database.IsSqlServer() ? SqlTerminate + identity : PostgresTerminate + identity + CloseArgument;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
