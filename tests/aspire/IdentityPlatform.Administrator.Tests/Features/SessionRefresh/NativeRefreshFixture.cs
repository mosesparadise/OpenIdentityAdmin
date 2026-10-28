using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Owns an isolated real database and native stage-only graph; it supplies no product grant, crypto or HTTP authentication shortcut.</summary>
internal sealed class NativeRefreshFixture(BootstrapDatabase database, ServiceProvider services, FactorClock clock, NativeRefreshRevocationFault fault, NativeRefreshConcurrencyObservation concurrency) : IAsyncDisposable
{
    internal IServiceProvider Services => services;
    internal FactorClock Clock => clock;
    internal NativeRefreshRevocationFault Fault => fault;
    internal NativeRefreshConcurrencyObservation Concurrency => concurrency;

    /// <summary>Uses existing migrated runtime grants while keeping the test descriptors separate from production protocol composition.</summary>
    internal static async Task<NativeRefreshFixture> CreateAsync(BootstrapFixture fixture, string provider, TimeSpan? leeway = null)
    {
        var database = await fixture.LeaseAsync(provider);
        try
        {
            var clock = new FactorClock(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            var fault = new NativeRefreshRevocationFault();
            var concurrency = new NativeRefreshConcurrencyObservation();
            var registrations = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
                .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), database.RuntimeConnection)
                .AddSingleton<TimeProvider>(clock).AddProtocolStorageOwnership();
            registrations.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fault, concurrency));
            NativeRefreshStages.Configure(registrations, leeway);
            return new(database, registrations.BuildServiceProvider(validateScopes: true), clock, fault, concurrency);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>Gives each operation a fresh bounded native owner; no transaction or tracked row crosses this boundary.</summary>
    internal Task<T> RunAsync<T>(Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action) =>
        new ProtocolStorageRequest(Clock, TestContext.Current.CancellationToken)
            .RunAsync(services.GetRequiredService<IServiceScopeFactory>(), action);

    /// <summary>Runs one explicit mutation epoch and releases a result only after commit and owned scope disposal.</summary>
    internal Task<T> CommitAsync<T>(Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action) => RunAsync(async (scope, token) =>
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var result = await action(scope, token);
        await transaction.CommitAsync(token);
        return result;
    });

    /// <summary>Disposes native services before dropping the disposable database, attempting both even after a failure.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await services.DisposeAsync();
        }
        finally { await database.DisposeAsync(); }
    }
}
