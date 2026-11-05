using IdentityPlatform.Infrastructure.Features.ApplicationGrants;

namespace IdentityPlatform.Worker.Features.ApplicationGrants;

/// <summary>Polls durable jobs without retaining scopes, live receipts, native entities or submitted credentials between batches.</summary>
internal sealed class GrantWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<GrantWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly Action<ILogger, Exception?> QueueUnavailable = LoggerMessage.Define(LogLevel.Warning, new EventId(2101, nameof(QueueUnavailable)), Unavailable);
    private static readonly Action<ILogger, Exception?> BatchUnavailable = LoggerMessage.Define(LogLevel.Warning, new EventId(2102, nameof(BatchUnavailable)), Unavailable);
    private const string Unavailable = "Application grant processing is unavailable; durable status remains authoritative.";

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

    /// <summary>A failed candidate cannot hide other queued work; errors expose neither entity identifiers nor native exception text.</summary>
    private async Task PollAsync(CancellationToken token)
    {
        foreach (var job in await GrantJobQueue.ReadAsync(scopes, clock, token))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await GrantBatchOperation.ExecuteAsync(scopes, job, clock, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { BatchUnavailable(logger, null); }
        }
    }
}
