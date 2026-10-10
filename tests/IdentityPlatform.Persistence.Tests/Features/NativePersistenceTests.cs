using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Npgsql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features;

public sealed class NativePersistenceTests
{
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public async Task Migrations_and_native_managers_preserve_state_on_repeated_initialization(
        DatabaseProvider provider)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var cancellationToken = deadline.Token;
        await using var postgres = new PostgreSqlBuilder(
            "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336")
            .WithDatabase("identity_test").Build();
        await using var sqlServer = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090")
            .Build();

        var connection = provider == DatabaseProvider.PostgreSql
            ? await StartPostgres() : await StartSqlServer();
        var services = new ServiceCollection().AddLogging();
        services.AddPlatformPersistence(provider, connection, connection);
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = serviceProvider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessions = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(serviceProvider, cancellationToken));

        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "CaseSensitiveClient",
            ClientType = OpenIddictConstants.ClientTypes.Public
        }, cancellationToken);
        await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "casesensitiveclient",
            ClientType = OpenIddictConstants.ClientTypes.Public
        }, cancellationToken);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = new PlatformUser { UserName = "synthetic-user", Email = "user@example.test" };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        // Synthetic storage data, not a WebAuthn attestation/authentication claim.
        var passkey = new UserPasskeyInfo([1, 2, 3], [4, 5, 6], DateTimeOffset.UtcNow, 0,
            ["internal"], true, false, false, [], []) { Name = "synthetic-passkey" };
        Assert.True((await users.AddOrUpdatePasskeyAsync(user, passkey)).Succeeded);
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        await scopes.CreateAsync(new OpenIddictScopeDescriptor { Name = "CaseSensitiveScope" }, cancellationToken);
        await scopes.CreateAsync(new OpenIddictScopeDescriptor { Name = "casesensitivescope" }, cancellationToken);
        Assert.Null(await scopes.FindByNameAsync("CASESENSITIVESCOPE", cancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ValidationException>(async () =>
            await scopes.CreateAsync(new OpenIddictScopeDescriptor { Name = "CaseSensitiveScope" }, cancellationToken));

        Assert.Equal(0, await DatabaseUpgrade.RunAsync(serviceProvider, cancellationToken));
        Assert.Empty(await database.Database.GetPendingMigrationsAsync(cancellationToken));
        Assert.Empty(await sessions.Database.GetPendingMigrationsAsync(cancellationToken));
        Assert.NotEmpty(await database.Database.GetAppliedMigrationsAsync(cancellationToken));
        Assert.NotEmpty(await sessions.Database.GetAppliedMigrationsAsync(cancellationToken));
        await using (var freshScope = serviceProvider.CreateAsyncScope())
        {
            var freshUsers = freshScope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var reloaded = await freshUsers.FindByIdAsync(user.Id);
            Assert.NotNull(reloaded);
            Assert.NotSame(user, reloaded);
            Assert.Equal("synthetic-passkey", Assert.Single(await freshUsers.GetPasskeysAsync(reloaded)).Name);
        }
        var first = await applications.FindByClientIdAsync("CaseSensitiveClient", cancellationToken);
        var second = await applications.FindByClientIdAsync("casesensitiveclient", cancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(await applications.GetIdAsync(first, cancellationToken),
            await applications.GetIdAsync(second, cancellationToken));
        Assert.Null(await applications.FindByClientIdAsync("CASESENSITIVECLIENT", cancellationToken));
        var runtimePassword = "aA1" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await LocalRuntimeRoleProvisioner.ProvisionAsync(database, RuntimeStore.Platform, runtimePassword, cancellationToken);
        await LocalRuntimeRoleProvisioner.ProvisionAsync(sessions, RuntimeStore.Sessions, runtimePassword, cancellationToken);
        var runtimeConnection = new DbConnectionStringBuilder { ConnectionString = connection };
        runtimeConnection[provider == DatabaseProvider.PostgreSql ? "Username" : "User ID"] = "platform_runtime";
        runtimeConnection["Password"] = runtimePassword;
        var runtimeServices = new ServiceCollection().AddLogging();
        runtimeServices.AddIdentityPersistence(provider, runtimeConnection.ConnectionString);
        await using var runtimeProvider = runtimeServices.BuildServiceProvider(validateScopes: true);
        var health = new SchemaReadinessCheck<PlatformDbContext>(runtimeProvider,
            SchemaCatalog.Platform(provider));
        Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new(), cancellationToken)).Status);
        await using (var runtimeScope = runtimeProvider.CreateAsyncScope())
        {
            var runtimeDb = runtimeScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var runtimeApps = runtimeScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            Assert.NotNull(await runtimeApps.FindByClientIdAsync("CaseSensitiveClient", cancellationToken));
            await runtimeApps.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "runtime-created", ClientType = OpenIddictConstants.ClientTypes.Public
            }, cancellationToken);
            AssertPermissionDenied(provider, await Assert.ThrowsAnyAsync<DbException>(() => runtimeDb.Database.ExecuteSqlRawAsync(
                "CREATE TABLE platform.MustNeverExist (Id integer)", cancellationToken)));
            AssertPermissionDenied(provider, await Assert.ThrowsAnyAsync<DbException>(() => runtimeDb.Database.ExecuteSqlRawAsync(
                provider == DatabaseProvider.PostgreSql
                    ? "DELETE FROM platform.\"PlatformMigrations\""
                    : "DELETE FROM platform.PlatformMigrations", cancellationToken)));
        }
        // BFF credentials cannot read the identity store, even if both contexts share a database.
        runtimeConnection[provider == DatabaseProvider.PostgreSql ? "Username" : "User ID"] = "sessions_runtime";
        var bffServices = new ServiceCollection().AddLogging();
        bffServices.AddSessionPersistence(provider, runtimeConnection.ConnectionString);
        await using var bffProvider = bffServices.BuildServiceProvider(validateScopes: true);
        Assert.Equal(HealthStatus.Healthy, (await new SchemaReadinessCheck<AdminSessionDbContext>(
            bffProvider, SchemaCatalog.Sessions(provider)).CheckHealthAsync(new(), cancellationToken)).Status);
        await using (var bffScope = bffProvider.CreateAsyncScope())
        {
            var bffDb = bffScope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
            var entry = new OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken
            {
                Id = Guid.NewGuid().ToString(), Type = "synthetic-client-state"
            };
            bffDb.Add(entry);
            await bffDb.SaveChangesAsync(cancellationToken);
            bffDb.ChangeTracker.Clear();
            Assert.NotNull(await bffDb.FindAsync<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>(
                [entry.Id], cancellationToken));
            AssertPermissionDenied(provider, await Assert.ThrowsAnyAsync<DbException>(() => bffDb.Database.ExecuteSqlRawAsync(
                "CREATE TABLE bff.MustNeverExist (Id integer)", cancellationToken)));
            AssertPermissionDenied(provider, await Assert.ThrowsAnyAsync<DbException>(() => bffDb.Database.ExecuteSqlRawAsync(
                provider == DatabaseProvider.PostgreSql ? "DELETE FROM bff.\"SessionMigrations\"" : "DELETE FROM bff.SessionMigrations", cancellationToken)));
        }
        var bffOptions = new DbContextOptionsBuilder<PlatformDbContext>();
        PersistenceRegistration.ConfigureDatabase(bffOptions, provider, runtimeConnection.ConnectionString, "PlatformMigrations");
        await using (var bffCannotReadIdentity = new PlatformDbContext(bffOptions.Options))
        {
            AssertPermissionDenied(provider, await Assert.ThrowsAnyAsync<DbException>(
                () => bffCannotReadIdentity.Users.CountAsync(cancellationToken)));
        }
        await using (var competingScope = serviceProvider.CreateAsyncScope())
        {
            var competing = competingScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var stale = await competing.FindByClientIdAsync("CaseSensitiveClient", cancellationToken);
            Assert.NotNull(stale);
            await applications.UpdateAsync(first, new OpenIddictApplicationDescriptor
            {
                ClientId = "CaseSensitiveClient", ClientType = OpenIddictConstants.ClientTypes.Public, DisplayName = "first-change"
            }, cancellationToken);
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await competing.UpdateAsync(stale, new OpenIddictApplicationDescriptor
                {
                    ClientId = "CaseSensitiveClient", ClientType = OpenIddictConstants.ClientTypes.Public, DisplayName = "stale-change"
                }, cancellationToken));
        }
        var incompatible = new SchemaReadinessCheck<PlatformDbContext>(runtimeProvider, ["unknown-version"]);
        Assert.Equal(HealthStatus.Unhealthy, (await incompatible.CheckHealthAsync(new(), cancellationToken)).Status);
        await Assert.ThrowsAsync<OpenIddictExceptions.ValidationException>(async () =>
            await applications.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "CaseSensitiveClient",
                ClientType = OpenIddictConstants.ClientTypes.Public
            }, cancellationToken));

        async Task<string> StartPostgres()
        {
            await postgres.StartAsync(cancellationToken);
            return postgres.GetConnectionString();
        }

        async Task<string> StartSqlServer()
        {
            await sqlServer.StartAsync(cancellationToken);
            return sqlServer.GetConnectionString();
        }
    }

    private static void AssertPermissionDenied(DatabaseProvider provider, DbException exception)
    {
        if (provider == DatabaseProvider.PostgreSql)
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(exception).SqlState);
        else
            Assert.Contains(Assert.IsType<SqlException>(exception).Number, new[] { 229, 262, 2760 });
    }
}
