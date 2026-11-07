using System.Runtime.ExceptionServices;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Releases the server barrier and joins owned exchanges without converting assertion, observer or teardown failures into success.</summary>
internal static class PilotReplayDistributionCleanup
{
    private const int CleanupSeconds = 15;
    private const string CleanupFailure = "Ordered pilot replay or its joined cleanup failed.";

    /// <summary>Retains the primary failure and attempts every cleanup stage; an uncertain server outcome invalidates the fixture.</summary>
    internal static async Task CompleteAsync(IssuerEnvironment environment, PilotReplayDatabaseBarrier barrier,
        Task<HttpResponseMessage>? winner, Task<HttpResponseMessage>? replay, ExceptionDispatchInfo? failure)
    {
        var errors = new List<Exception>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        await AttemptAsync(() => barrier.ReleaseAsync(deadline.Token), errors);
        var requests = new[] { winner, replay }.OfType<Task<HttpResponseMessage>>().ToArray();
        await AttemptAsync(async () => { await Task.WhenAll(requests).WaitAsync(deadline.Token); }, errors);
        foreach (var request in requests.Where(value => value.IsCompletedSuccessfully))
        {
            request.Result.Dispose();
        }
        await AttemptAsync(() => barrier.DrainAsync(deadline.Token), errors);
        await AttemptAsync(() => barrier.DisposeAsync().AsTask(), errors);
        if (errors.Count != 0)
        {
            environment.Invalidate();
            if (failure is not null)
            {
                errors.Insert(0, failure.SourceException);
            }
            throw new AggregateException(CleanupFailure, errors);
        }
        failure?.Throw();
    }

    /// <summary>Records cleanup failure while allowing subsequent owned resources to be released.</summary>
    private static async Task AttemptAsync(Func<Task> cleanup, List<Exception> errors)
    {
        try
        {
            await cleanup();
        }
        catch (Exception error) { errors.Add(error); }
    }
}
