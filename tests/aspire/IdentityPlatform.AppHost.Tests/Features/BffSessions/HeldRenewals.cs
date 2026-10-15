using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Testing;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Ensures every started barrier-held renewal is cancelled/released and observed before providers are disposed.</summary>
internal sealed class HeldRenewals(TicketWriteBarrier barrier, CancellationToken cancellationToken) : IAsyncDisposable
{
    private static readonly TimeSpan DrainDeadline = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    private readonly List<Task<SessionTicketResult>> tasks = [];

    /// <summary>Starts one owned renewal with the shared cancellation lifetime.</summary>
    internal Task<SessionTicketResult> Start(SessionTicketStore store, string handle)
    {
        var task = store.RenewAsync(handle, 1, BffSessionFixture.Ticket(), lifetime.Token);
        tasks.Add(task);
        return task;
    }

    /// <summary>Drains native work even when the test assertion fails before release; it never leaves a task behind a disposed timer.</summary>
    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        barrier.Release();
        try
        {
            await Task.WhenAll(tasks).WaitAsync(DrainDeadline);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { lifetime.Dispose(); }
    }
}
