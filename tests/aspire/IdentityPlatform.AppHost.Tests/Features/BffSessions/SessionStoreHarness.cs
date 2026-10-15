using System.Data.Common;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Runs real session stores against the native Aspire Migrator and session-only runtime principal.</summary>
internal sealed class SessionStoreHarness : IAsyncDisposable
{
    private const string Development = "--environment=Development";
    private const string ProviderArgument = "--Database:Provider=";
    private const string SessionResource = "adminsessions";
    private const string BffResource = "admin-bff";
    private const string PasswordParameter = "sessions-runtime-password";
    private const string RuntimePrincipal = "sessions_runtime";
    private const string PostgresUser = "Username";
    private const string SqlUser = "User ID";
    private const string PasswordField = "Password";
    private const string History = "SessionMigrations";
    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? application;
    private string ownerConnection = string.Empty;
    private string runtimeConnection = string.Empty;
    private DatabaseProvider provider;
    internal SessionLogCapture Logs { get; } = new();
    internal BffSessionFixture Material { get; } = new();
    internal BffSessionClock Clock { get; } = new(BffSessionFixture.Start);

    /// <summary>Starts the unchanged production topology and waits for actual BFF schema readiness before exposing test-only stores.</summary>
    internal static async Task<SessionStoreHarness> StartAsync(string providerName, CancellationToken cancellationToken)
    {
        var harness = new SessionStoreHarness { provider = DatabaseProviderParser.Parse(providerName) };
        try
        {
            harness.builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                [Development, ProviderArgument + providerName], cancellationToken);
            harness.application = await harness.builder.BuildAsync(cancellationToken);
            await harness.application.StartAsync(cancellationToken);
            await harness.application.ResourceNotifications.WaitForResourceHealthyAsync(BffResource, WaitBehavior.StopOnResourceUnavailable, cancellationToken);
            harness.ownerConnection = (await harness.application.GetConnectionStringAsync(SessionResource, cancellationToken))!;
            var password = await harness.application.Services.GetRequiredService<DistributedApplicationModel>().Resources
                .OfType<ParameterResource>().Single(resource => resource.Name == PasswordParameter).GetValueAsync(cancellationToken);
            var runtime = new DbConnectionStringBuilder { ConnectionString = harness.ownerConnection };
            runtime[providerName == nameof(DatabaseProvider.PostgreSql) ? PostgresUser : SqlUser] = RuntimePrincipal;
            runtime[PasswordField] = password;
            harness.runtimeConnection = runtime.ConnectionString;
            return harness;
        }
        catch { await harness.DisposeAsync(); throw; }
    }

    /// <summary>Creates independently owned service/ring providers sharing only the real database and persistent ring files.</summary>
    internal SessionStoreNode Node(params IInterceptor[] interceptors) => Node(Clock, interceptors);

    /// <summary>Allows independent deterministic clocks when testing cross-instance observation and cleanup races.</summary>
    internal SessionStoreNode Node(TimeProvider clock, params IInterceptor[] interceptors)
    {
        var protection = Material.Protection();
        try
        {
            var services = new ServiceCollection().AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
            services.AddSessionPersistence(provider, runtimeConnection);
            services.AddDbContext<AdminSessionDbContext>(options => options.AddInterceptors(interceptors));
            services.AddSessionTickets(protection, clock);
            return new SessionStoreNode(services.BuildServiceProvider(validateScopes: true), protection);
        }
        catch { protection.Dispose(); throw; }
    }

    /// <summary>Provides test-only owner access for persisted-state assertions and controlled native locks; production never receives it.</summary>
    internal AdminSessionDbContext Database(bool runtime = false)
    {
        var options = new DbContextOptionsBuilder<AdminSessionDbContext>();
        PersistenceRegistration.ConfigureDatabase(options, provider, runtime ? runtimeConnection : ownerConnection, History);
        return new(options.Options);
    }

    /// <summary>Stops only this fixture's actual database to exercise operational failure without connection-string substitutions.</summary>
    internal async Task StopDatabaseAsync(CancellationToken cancellationToken)
    {
        const string DatabaseResource = "database";
        var model = application!.Services.GetRequiredService<DistributedApplicationModel>();
        await application.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(
            model.Resources.Single(resource => resource.Name == DatabaseResource), KnownResourceCommands.StopCommand, cancellationToken);
        await application.ResourceNotifications.WaitForResourceAsync(DatabaseResource, KnownResourceStates.Exited, cancellationToken);
    }

    /// <summary>Stops owned Aspire resources before deleting synthetic protection material, including partial-startup failure.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (application is not null)
            {
                await application.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (builder is not null)
                {
                    await builder.DisposeAsync();
                }
            }
            finally { Material.Dispose(); }
        }
    }
}

/// <summary>Owns one independent in-process consumer; this fixture does not claim process-crash recovery.</summary>
internal sealed class SessionStoreNode(ServiceProvider services, SessionProtection protection) : IAsyncDisposable
{
    internal SessionTicketStore Store => services.GetRequiredService<SessionTicketStore>();

    /// <summary>Releases database scopes and store consumers before their wrapping credentials.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await services.DisposeAsync();
        }
        finally { protection.Dispose(); }
    }
}
