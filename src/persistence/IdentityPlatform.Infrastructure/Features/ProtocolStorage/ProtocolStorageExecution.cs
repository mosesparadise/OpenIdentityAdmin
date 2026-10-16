namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Links caller cancellation to an epoch while retaining the exclusive execution slot until actual completion.</summary>
internal sealed class ProtocolStorageExecution : IDisposable
{
    private readonly ProtocolStorageOwner owner;
    private readonly ProtocolStoragePhase phase;
    private readonly CancellationTokenSource cancellation;
    internal CancellationToken Token => cancellation.Token;

    /// <summary>Links normal native work to the phase; cleanup uses independent cancellation after ordinary work has drained.</summary>
    internal ProtocolStorageExecution(ProtocolStorageOwner owner, ProtocolStoragePhase phase, bool cleanup, CancellationToken caller)
    {
        this.owner = owner;
        this.phase = phase;
        cancellation = cleanup ? new() : CancellationTokenSource.CreateLinkedTokenSource(phase.Lifetime.Token, caller);
    }

    /// <summary>Rechecks every original cancellation/time/ownership boundary after native work finishes.</summary>
    internal void RequireCurrent() => owner.Check(phase, Token);

    /// <summary>Releases occupancy only when the surrounding awaited operation or native cleanup is finished.</summary>
    public void Dispose()
    {
        cancellation.Dispose();
        owner.Exit();
    }
}
