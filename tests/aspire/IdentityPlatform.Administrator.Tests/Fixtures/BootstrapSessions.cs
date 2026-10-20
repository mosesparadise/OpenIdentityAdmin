using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Retains a Sessions-only runtime connection in memory and drops its disposable database after all clients finish.</summary>
internal sealed class BootstrapSessions(DatabaseProvider provider, string runtimeConnection, Func<Task> drop, Func<int, int, CancellationToken, Task> observe, BootstrapSessionAccess access) : IAsyncDisposable
{
    internal BootstrapSessionAccess Access => access;

    /// <summary>Registers the tested client using only its least-privilege Sessions principal.</summary>
    internal IServiceCollection AddPersistence(IServiceCollection services) => services.AddSessionPersistence(provider, runtimeConnection);

    /// <summary>Observes only exact session blocking through the fixture's separate privileged connection; runtime rights stay unchanged.</summary>
    internal Task ObserveAsync(int session, int blocker, CancellationToken token) => observe(session, blocker, token);

    /// <summary>Runs privileged migration/provisioning before exposing a runtime lease; credentials never enter diagnostics.</summary>
    internal static async Task InitializeAsync(DatabaseProvider provider, string connection, string password, CancellationToken token)
    {
        await using var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
            .AddSessionPersistence(provider, connection).BuildServiceProvider(validateScopes: true);
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        await database.Database.MigrateAsync(token);
        await LocalRuntimeRoleProvisioner.ProvisionAsync(database, RuntimeStore.Sessions, password, token);
    }

    /// <summary>Drops only the fixture-owned database after caller-owned native service roots have been disposed.</summary>
    public ValueTask DisposeAsync() => new(drop());
}
