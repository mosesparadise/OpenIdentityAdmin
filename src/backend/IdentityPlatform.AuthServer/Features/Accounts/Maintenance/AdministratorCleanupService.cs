using System.Diagnostics.Metrics;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Maintenance;

/// <summary>Schedules bounded metadata reclamation without decrypting secrets or selecting an in-memory leader.</summary>
internal sealed class AdministratorCleanupService(IServiceScopeFactory scopes, TimeProvider time) : BackgroundService
{
    private const string MeterName = "IdentityPlatform.Administrator.Cleanup";
    private const string DeletedName = "administrator.cleanup.deleted";
    private const string DeferredName = "administrator.cleanup.deferred";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Deleted = Meter.CreateCounter<long>(DeletedName);
    private static readonly Counter<long> Deferred = Meter.CreateCounter<long>(DeferredName);

    /// <summary>Runs at the approved interval; a contested or failed batch waits for the next tick without sensitive retries.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(AdministratorPolicy.CleanupSeconds), time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>Owns a fresh scope per pass and records only fixed low-cardinality counts; provider exceptions are never logged.</summary>
    internal async Task RunOnceAsync(CancellationToken token)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<CeremonyCleanup>().RunAsync(token);
            Deleted.Add(count);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            Deferred.Add(1);
        }
    }
}
