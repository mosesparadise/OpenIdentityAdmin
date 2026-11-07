using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Worker.Features.Catalogue;

/// <summary>Polls durable jobs without retaining scopes, live receipts, native entities or submitted credentials between batches.</summary>
internal sealed class CatalogueDeletionWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<CatalogueDeletionWorker> logger) : BackgroundService
{
    private Guid? after;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly Action<ILogger, Exception?> QueueUnavailable = LoggerMessage.Define(LogLevel.Warning, new EventId(2111, nameof(QueueUnavailable)), Unavailable);
    private static readonly Action<ILogger, Exception?> BatchUnavailable = LoggerMessage.Define(LogLevel.Warning, new EventId(2112, nameof(BatchUnavailable)), Unavailable);
    private const string Unavailable = "Catalogue deletion processing is unavailable; durable status remains authoritative.";

    /// <summary>Bounds each queue and batch operation independently; shutdown cancellation prevents admission of further work.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await PollAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch { QueueUnavailable(logger, null); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>Keyset progress moves past failed candidates and wraps after exhaustion; errors expose neither entity identifiers nor native exception text.</summary>
    private async Task PollAsync(CancellationToken token)
    {
        var jobs = await CatalogueDeletionQueue.ReadAsync(scopes, clock, after, token);
        after = jobs.Length == 0 ? null : jobs[^1];
        foreach (var job in jobs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await CatalogueDeletionProcessor.ExecuteAsync(scopes, job, clock, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { BatchUnavailable(logger, null); }
        }
    }
}
