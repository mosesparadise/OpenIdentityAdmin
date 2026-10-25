using System.Runtime.ExceptionServices;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Retains one original request budget across sequential fresh native scopes without owning shared cancellation resources.</summary>
internal sealed class ProtocolStorageRequest(TimeProvider clock, CancellationToken token, Action? publicationGuard = null)
{
    private readonly long started = clock.GetTimestamp();
    private readonly object sync = new();
    private readonly List<ProtocolStorageScope> scopes = [];
    private readonly List<Task> acquisitionCleanup = [];
    private readonly List<ProtocolStoragePhase> phases = [];
    private ProtocolResponseCapture? response;
    private ProtocolResponseOutput? errorOutput;
    private ProtocolStorageCheckpoint? errorCheckpoint;
    private JourneyLogout.Receipt? logout;
    private ProtocolResponseOutput? logoutOutput;
    private int active;
    private int faulted;
    internal TimeProvider Clock => clock;
    internal CancellationToken Token => token;
    internal TimeSpan Remaining => ProtocolStorageLifetime.RequestLimit - clock.GetElapsedTime(started);

    /// <summary>Checks the absolute original budget; a child cannot renew it or recover a failed request.</summary>
    internal void RequireCurrent()
    {
        if (Volatile.Read(ref faulted) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        token.ThrowIfCancellationRequested();
        if (Remaining <= TimeSpan.Zero)
        {
            throw new OperationCanceledException(token);
        }
    }

    /// <summary>Runs one whole native flow using only its owned child services; actual completion and cleanup precede another scope.</summary>
    internal async Task<T> RunAsync<T>(IServiceScopeFactory factory, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> operation)
    {
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
        {
            Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        try
        {
            RequireCurrent();
            var result = await ExecuteAsync(factory, operation);
            RequireCurrent();
            return result;
        }
        catch { Fail(); throw; }
        finally { Volatile.Write(ref active, 0); }
    }

    /// <summary>Preserves both primary and cleanup failures while retaining the existing scope quarantine behavior.</summary>
    private async Task<T> ExecuteAsync<T>(IServiceScopeFactory factory, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> operation)
    {
        var scope = await ProtocolStorageScope.OpenAsync(factory, token, this);
        Exception? failure = null;
        T result = default!;
        var previous = ProtocolStorageAmbient.Current;
        try
        {
            ProtocolStorageAmbient.Current = new(this, scope);
            result = await operation(scope, scope.Owner.RequestToken);
            scope.Owner.RequireCompleted();
            RequireCurrent();
        }
        catch (Exception error) { failure = error; }
        finally { ProtocolStorageAmbient.Current = previous; }
        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception cleanup)
        {
            failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result;
    }

    /// <summary>Retains actual scope completion even when bounded caller cleanup enters quarantine.</summary>
    internal void Track(ProtocolStorageScope scope)
    {
        lock (sync)
        {
            scopes.Add(scope);
        }
    }

    /// <summary>Retains failed pre-owner acquisition cleanup until the live HTTP request can safely return.</summary>
    internal void Track(Task cleanup)
    {
        lock (sync)
        {
            acquisitionCleanup.Add(cleanup);
        }
    }

    /// <summary>Records every write phase before native transaction acquisition can begin.</summary>
    internal void Track(ProtocolStoragePhase phase)
    {
        lock (sync)
        {
            phases.Add(phase);
        }
    }

    /// <summary>Joins all actual cleanup without detaching a live HTTP context after a caller timeout.</summary>
    internal async Task DrainAsync()
    {
        Task[] pending;
        lock (sync)
        {
            pending = scopes.Select(scope => scope.ActualCompletion).Concat(acquisitionCleanup).ToArray();
        }
        await Task.WhenAll(pending);
    }

    /// <summary>Binds one root HTTP capture; a later child or another HTTP boundary cannot replace its publication destination.</summary>
    internal void BindResponse(ProtocolResponseCapture capture)
    {
        RequireCurrent();
        if (Interlocked.CompareExchange(ref response, capture, null) is not null || !capture.BelongsTo(this))
        {
            Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Captures only the coordinator's exact recognized phase sequence after actual transactional child cleanup.</summary>
    internal ProtocolStorageCheckpoint CaptureErrorCheckpoint(IReadOnlyList<ProtocolStoragePhase> recognized)
    {
        RequireCurrent();
        lock (sync)
        {
            if (!phases.SequenceEqual(recognized, ReferenceEqualityComparer.Instance))
            {
                Fail();
                throw ProtocolStorageOwner.Unavailable();
            }
            return new(this, phases);
        }
    }

    /// <summary>Accepts one privately verified native error output; a generic error outcome never calls this admission method.</summary>
    internal void AdmitError(ProtocolResponseOutput output, ProtocolStorageCheckpoint checkpoint)
    {
        RequireCurrent();
        lock (sync)
        {
            if (errorOutput is not null || response is null || !checkpoint.Matches(this, phases))
            {
                Fail();
                throw ProtocolStorageOwner.Unavailable();
            }
            output.RequireFrozenCapture(this, response);
            errorOutput = output;
            errorCheckpoint = checkpoint;
        }
    }

    /// <summary>Admits only a real completed issuer logout and its frozen output, with no unrelated native write phase.</summary>
    internal void AdmitIssuerLogout(ProtocolResponseOutput output, JourneyLogout.Receipt receipt)
    {
        receipt.RequireCurrent(this);
        lock (sync)
        {
            if (logout is not null || errorOutput is not null || response is null || phases.Count != 0)
            {
                Fail();
                throw ProtocolStorageOwner.Unavailable();
            }
            output.RequireFrozenCapture(this, response);
            logout = receipt;
            logoutOutput = output;
        }
    }

    /// <summary>Separates read-only responses, committed writes and native errors without treating rollback as success.</summary>
    internal void RequirePublication(ProtocolResponseOutcome outcome)
    {
        RequireCurrent();
        try
        {
            publicationGuard?.Invoke();
        }
        catch { Fail(); throw; }
        lock (sync)
        {
            if (outcome == ProtocolResponseOutcome.VerifiedIssuerLogout && logout is not null && logoutOutput is not null &&
                response is not null && phases.Count == 0 && errorOutput is null)
            {
                logout.RequireCurrent(this);
                logoutOutput.RequireFrozenCapture(this, response);
                return;
            }
            if (outcome == ProtocolResponseOutcome.VerifiedNativeError && errorOutput is not null && response is not null &&
                errorCheckpoint?.Matches(this, phases) == true)
            {
                errorOutput.RequireFrozenCapture(this, response);
                return;
            }
            if (outcome == ProtocolResponseOutcome.NativeError)
            {
                return;
            }
            if (outcome == ProtocolResponseOutcome.ReadOnly && phases.Count == 0)
            {
                return;
            }
            if (outcome == ProtocolResponseOutcome.Committed && phases.Count != 0 && phases.All(phase => phase.Committed))
            {
                return;
            }
        }
        Fail();
        throw ProtocolStorageOwner.Unavailable();
    }

    /// <summary>Prevents any later child from treating a swallowed owner failure as a fresh request.</summary>
    internal void Fail() => Interlocked.Exchange(ref faulted, 1);
}
