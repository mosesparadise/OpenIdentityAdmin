using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.AppHost.Tests.Features;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

[CollectionDefinition("administrator-bootstrap")]
public sealed class BootstrapTestGroup : ICollectionFixture<BootstrapFixture>;

public sealed class BootstrapFixture : IAsyncLifetime
{
    /// <summary>Uses the already running provider graph and its existing Sessions runtime password.</summary>
    internal async Task<BootstrapSessions> LeaseSessionsAsync(string provider) =>
        await (await HostAsync(provider)).LeaseSessionsAsync(TestContext.Current.CancellationToken);

    private readonly Dictionary<string, BootstrapHost> hosts = new(StringComparer.Ordinal);
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>Leases one isolated Platform database from the existing shared provider graph.</summary>
    public async Task<BootstrapDatabase> LeaseAsync(string provider)
        => await (await HostAsync(provider)).LeaseAsync(TestContext.Current.CancellationToken);

    /// <summary>Shares only the running provider graph; each caller still receives its own disposable database.</summary>
    private async Task<BootstrapHost> HostAsync(string provider)
    {
        if (!hosts.TryGetValue(provider, out var host))
        {
            host = await BootstrapHost.CreateAsync(provider, TestContext.Current.CancellationToken);
            hosts.Add(provider, host);
        }
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        var failed = false;
        foreach (var host in hosts.Values)
        {
            try
            {
                await host.DisposeAsync();
            }
            catch (Exception) { failed = true; }
        }
        Assert.False(failed, "Bootstrap host cleanup failed.");
    }
}

public sealed class BootstrapHost : IAsyncDisposable
{
    private const string SessionsPasswordParameter = "sessions-runtime-password";
    private const string SessionsRuntimePrincipal = "sessions_runtime";

    /// <summary>Creates a distinct database, migrates only Sessions and preserves the host's current runtime login password.</summary>
    internal async Task<BootstrapSessions> LeaseSessionsAsync(CancellationToken token)
    {
        var name = DatabasePrefix + Guid.NewGuid().ToString(IdentifierFormat);
        await using var connection = Connection(administratorConnection);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = DatabaseCommandSeconds;
        command.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            provider == DatabaseProvider.PostgreSql ? PostgreSqlCreate : SqlServerCreate, name);
        await command.ExecuteNonQueryAsync(token);
        try
        {
            var model = application!.Services.GetRequiredService<DistributedApplicationModel>();
            var password = (await model.Resources.OfType<ParameterResource>()
                .Single(value => value.Name == SessionsPasswordParameter).GetValueAsync(token))!;
            var admin = WithDatabase(administratorConnection, name);
            var runtime = new DbConnectionStringBuilder { ConnectionString = admin };
            runtime[provider == DatabaseProvider.PostgreSql ? PostgreSqlUserKey : SqlServerUserKey] = SessionsRuntimePrincipal;
            runtime[PasswordKey] = password;
            await BootstrapSessions.InitializeAsync(provider, admin, password, token);
            var observer = new IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility.NativeLockObservation(
                provider == DatabaseProvider.PostgreSql ? nameof(DatabaseProvider.PostgreSql) : nameof(DatabaseProvider.SqlServer), admin);
            return new(provider, runtime.ConnectionString, () => DropAsync(name), observer.WaitAsync, new(provider, admin, runtime.ConnectionString));
        }
        catch
        {
            await DropAsync(name);
            throw new InvalidOperationException(DatabaseFailure);
        }
    }

    private const int StartupMinutes = 3;
    private const int DatabaseCommandSeconds = 15;
    private const string DevelopmentArgument = "--environment=Development";
    private const string ProviderArgumentPrefix = "--Database:Provider=";
    private const string DisabledPilotArgument = "--Pilot:Enabled=false";
    private const string AuthResource = "auth";
    private const string PlatformDatabase = "platform";
    private const string RuntimePasswordParameter = "platform-runtime-password";
    private const string StartupFailure = "Bootstrap harness startup failed.";
    private const string DatabasePrefix = "administrator_";
    private const string IdentifierFormat = "N";
    private const string PostgreSqlCreate = "CREATE DATABASE \"{0}\"";
    private const string SqlServerCreate = "CREATE DATABASE [{0}]";
    private const string PostgreSqlUserKey = "Username";
    private const string SqlServerUserKey = "User ID";
    private const string RuntimePrincipal = "platform_runtime";
    private const string PasswordKey = "Password";
    private const string DatabaseFailure = "Bootstrap database setup failed.";

    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? application;
    private LifecycleEvidence? lifecycle;
    private string administratorConnection = "";
    private string runtimePassword = "";
    private DatabaseProvider provider;

    /// <summary>Starts the existing real provider graph within its linked startup deadline; failure disposes the owned host and retains the fixed harness error.</summary>
    public static async Task<BootstrapHost> CreateAsync(string providerName, CancellationToken cancellationToken)
    {
        var host = new BootstrapHost { provider = DatabaseProviderParser.Parse(providerName) };
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startup.CancelAfter(TimeSpan.FromMinutes(StartupMinutes));
            SetupDiagnostics.Write(SetupStage.HostBuilder);
            host.builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                [DevelopmentArgument, ProviderArgumentPrefix + providerName, DisabledPilotArgument], startup.Token);
            SetupDiagnostics.Write(SetupStage.HostBuild);
            host.application = await host.builder.BuildAsync(startup.Token);
            SetupDiagnostics.Write(SetupStage.HostStart);
            await host.application.StartAsync(startup.Token);
            SetupDiagnostics.Write(SetupStage.AuthReadiness);
            await host.application.ResourceNotifications.WaitForResourceHealthyAsync(AuthResource, startup.Token);
            host.lifecycle = LifecycleEvidence.Capture(host.application);
            host.administratorConnection = (await host.application.GetConnectionStringAsync(PlatformDatabase, startup.Token))!;
            var model = host.application.Services.GetRequiredService<DistributedApplicationModel>();
            host.runtimePassword = (await model.Resources.OfType<ParameterResource>()
                .Single(value => value.Name == RuntimePasswordParameter).GetValueAsync(startup.Token))!;
            SetupDiagnostics.Write(SetupStage.HostReady);
            return host;
        }
        catch
        {
            SetupDiagnostics.Write(SetupStage.HostCleanup);
            await host.DisposeAsync();
            throw new InvalidOperationException(StartupFailure);
        }
    }

    /// <summary>Creates and migrates a disposable provider database, retaining the existing least-privilege provisioning and cleanup behavior.</summary>
    public async Task<BootstrapDatabase> LeaseAsync(CancellationToken cancellationToken)
    {
        // The name is generated internally; no user-controlled identifier enters DDL.
        var name = DatabasePrefix + Guid.NewGuid().ToString(IdentifierFormat);
        await using var connection = Connection(administratorConnection);
        SetupDiagnostics.Write(SetupStage.LeaseOpen);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = DatabaseCommandSeconds;
        command.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture, provider == DatabaseProvider.PostgreSql ? PostgreSqlCreate : SqlServerCreate, name);
        SetupDiagnostics.Write(SetupStage.LeaseCreate);
        await command.ExecuteNonQueryAsync(cancellationToken);
        var admin = WithDatabase(administratorConnection, name);
        var runtime = new DbConnectionStringBuilder { ConnectionString = admin };
        runtime[provider == DatabaseProvider.PostgreSql ? PostgreSqlUserKey : SqlServerUserKey] = RuntimePrincipal;
        runtime[PasswordKey] = runtimePassword;
        var database = new BootstrapDatabase(provider, admin, runtime.ConnectionString, () => DropAsync(name));
        try
        {
            await using var scope = database.Administrator.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            SetupDiagnostics.Write(SetupStage.LeaseMigrate);
            await context.Database.MigrateAsync(cancellationToken);
            // Reuse the host's generated login password; changing it would disrupt other databases.
            SetupDiagnostics.Write(SetupStage.LeaseProvision);
            await LocalRuntimeRoleProvisioner.ProvisionAsync(context, RuntimeStore.Platform, runtimePassword, cancellationToken);
            SetupDiagnostics.Write(SetupStage.LeaseReady);
            return database;
        }
        catch
        {
            SetupDiagnostics.Write(SetupStage.LeaseCleanup);
            await database.DisposeAsync();
            throw new InvalidOperationException(DatabaseFailure);
        }
    }

    private string WithDatabase(string connection, string name) => provider == DatabaseProvider.PostgreSql
        ? new NpgsqlConnectionStringBuilder(connection) { Database = name, Pooling = false, Timeout = 5 }.ConnectionString
        : new SqlConnectionStringBuilder(connection) { InitialCatalog = name, Pooling = false, ConnectTimeout = 5 }.ConnectionString;

    private DbConnection Connection(string connection) => provider == DatabaseProvider.PostgreSql
        ? new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString)
        : new SqlConnection(new SqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);

    private async Task DropAsync(string name)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = Connection(administratorConnection);
        await connection.OpenAsync(cleanup.Token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 15;
        command.CommandText = provider == DatabaseProvider.PostgreSql ? $"DROP DATABASE \"{name}\"" : $"DROP DATABASE [{name}]";
        await command.ExecuteNonQueryAsync(cleanup.Token);
    }

    public async ValueTask DisposeAsync()
    {
        var failed = false;
        async Task Attempt(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception) { failed = true; }
        }
        if (application is not null)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await Attempt(() => application.StopAsync(cleanup.Token));
            await Attempt(() => application.DisposeAsync().AsTask());
        }
        if (builder is not null)
        {
            await Attempt(() => builder.DisposeAsync().AsTask());
        }
        if (lifecycle is not null)
        {
            await Attempt(lifecycle.AssertRemovedAsync);
        }
        Assert.False(failed, "Bootstrap host cleanup failed; all resources were attempted.");
    }
}

public sealed class BootstrapDatabase : IAsyncDisposable
{
    private readonly Func<Task> drop;
    private readonly DatabaseProvider provider;
    private readonly string runtimeConnection;
    internal string RuntimeConnection => runtimeConnection;
    internal string AdministratorConnection
    {
        get;
    }
    public string Username { get; } = "bootstrap.synthetic";
    public string Password { get; } = "aA1" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    public Guid OperationId { get; } = Guid.NewGuid();
    public ServiceProvider Administrator
    {
        get;
    }
    public ServiceProvider Runtime
    {
        get;
    }

    public BootstrapDatabase(DatabaseProvider provider, string administratorConnection, string runtimeConnection, Func<Task> drop)
    {
        this.provider = provider;
        this.runtimeConnection = runtimeConnection;
        AdministratorConnection = administratorConnection;
        this.drop = drop;
        Administrator = Services(administratorConnection);
        Runtime = Services(runtimeConnection);
    }

    public ServiceProvider WithInterceptor(IInterceptor interceptor) => Services(runtimeConnection, interceptor);

    public ServiceProvider WithRejectedUsers() => Services(runtimeConnection, rejectUsers: true);

    private ServiceProvider Services(string connection, IInterceptor? interceptor = null, bool rejectUsers = false)
    {
        var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
            .AddIdentityPersistence(provider, connection).AddAdministratorBootstrap();
        if (rejectUsers)
        {
            services.AddScoped<IUserValidator<PlatformUser>, RejectUserValidator>();
        }
        services.ConfigureDbContext<PlatformDbContext>(options =>
        {
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    public byte[] Input(string? password = null, Guid? operation = null, string? username = null) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        operationId = (operation ?? OperationId).ToString("D"),
        operatorReference = "synthetic-operator",
        userName = username ?? Username,
        password = password ?? Password
    });

    public async Task<BootstrapOutcome> InitializeAsync(CancellationToken cancellationToken, IServiceProvider? services = null, byte[]? input = null)
    {
        using var stream = new MemoryStream(input ?? Input());
        var request = await BootstrapInput.ReadAsync(stream, cancellationToken);
        await using var scope = (services ?? Runtime).CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdministratorBootstrap>().InitializeAsync(request, cancellationToken);
    }

    public async Task AssertRuntimeDrainedAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var scope = Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var connection = database.Database.GetDbConnection();
        await connection.OpenAsync(cleanup.Token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 3;
        command.CommandText = provider == DatabaseProvider.PostgreSql
            ? "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = current_database() AND usename = 'platform_runtime'"
            : "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id = DB_ID() AND login_name = 'platform_runtime'";
        while (Convert.ToInt32(await command.ExecuteScalarAsync(cleanup.Token), System.Globalization.CultureInfo.InvariantCulture) != 0)
        {
            await Task.Delay(50, cleanup.Token);
        }
    }

    public async Task AuditPermissionAsync(bool allow, CancellationToken cancellationToken)
    {
        await using var scope = Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sql = provider == DatabaseProvider.PostgreSql
            ? allow ? "GRANT INSERT ON platform.\"SecurityAuditIntent\" TO platform_runtime" : "REVOKE INSERT ON platform.\"SecurityAuditIntent\" FROM platform_runtime"
            : allow ? "REVOKE INSERT ON OBJECT::[platform].[SecurityAuditIntent] FROM [platform_runtime]" : "DENY INSERT ON OBJECT::[platform].[SecurityAuditIntent] TO [platform_runtime]";
        await database.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var failed = false;
        try
        {
            await Runtime.DisposeAsync();
        }
        catch (Exception) { failed = true; }
        try
        {
            await Administrator.DisposeAsync();
        }
        catch (Exception) { failed = true; }
        try
        {
            await drop();
        }
        catch (Exception) { failed = true; }
        Assert.False(failed, "Bootstrap database cleanup failed.");
    }
}

public sealed class RejectUserValidator(PlatformDbContext database) : IUserValidator<PlatformUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<PlatformUser> manager, PlatformUser user)
    {
        Assert.Equal(4, await database.Roles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(57, await database.RoleClaims.CountAsync(TestContext.Current.CancellationToken));
        return IdentityResult.Failed(new IdentityError { Code = "SyntheticReject", Description = "Synthetic rejection." });
    }
}
