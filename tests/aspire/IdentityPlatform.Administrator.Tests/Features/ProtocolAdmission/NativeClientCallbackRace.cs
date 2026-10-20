using System.Data.Common;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes real client-state coordination without replacing a provider command, lock, transaction or native manager.</summary>
internal sealed class NativeClientCallbackRace : DbCommandInterceptor
{
    private const int CoordinationRows = 1;
    internal TaskCompletionSource<int>? BeforeLock
    {
        get; set;
    }
    internal TaskCompletionSource<int>? Locked
    {
        get; set;
    }
    internal Task? Release
    {
        get; set;
    }

    /// <summary>Installs this root's independent test observer into its actual Sessions context.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<AdminSessionDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Captures the live peer session immediately before its coordination update can block.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (IsCoordination(command, eventData))
        {
            BeforeLock?.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Holds only the winning invocation after the real provider has granted the actual state lock.</summary>
    public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (IsCoordination(command, eventData) && Locked is { } signal)
        {
            Assert.Equal(CoordinationRows, result);
            Locked = null;
            signal.TrySetResult(NativeLockObservation.Session(command.Connection!));
            await (Release ?? Task.CompletedTask).WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Identifies the current provider's mapped coordination table inside a real EF transaction.</summary>
    private static bool IsCoordination(DbCommand command, CommandEventData data) => data.Context?.Database.CurrentTransaction is not null &&
        data.Context.Model.FindEntityType(typeof(AdministratorClientStateBudget))?.GetTableName() is { } table &&
        command.CommandText.Contains(table, StringComparison.Ordinal);

    /// <summary>Requires a provider-observed blocked peer before release; every exceptional path releases and joins both native callbacks.</summary>
    internal static async Task<NativeClientHttpResult[]> InvokeAsync(BootstrapSessions database, NativeClientHttp first, NativeClientHttp second,
        NativeClientCallbackRace winnerProbe, NativeClientCallbackRace peerProbe, Uri callback, string cookie)
    {
        var token = TestContext.Current.CancellationToken;
        var reserved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        winnerProbe.Locked = reserved;
        winnerProbe.Release = release.Task;
        var winner = first.InvokeAsync(callback, cookie);
        Task<NativeClientHttpResult>? peer = null;
        Task? waiting = null;
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var observed = false;
        var phase = NativeRaceCleanup.Phase.WinnerLock;
        ExceptionDispatchInfo? failure = null;
        try
        {
            await RequireSignalAsync(reserved.Task, winner, token);
            phase = NativeRaceCleanup.Phase.PeerLock;
            var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            peerProbe.BeforeLock = session;
            peer = second.InvokeAsync(callback, cookie);
            await RequireSignalAsync(session.Task, peer, token, winner);
            phase = NativeRaceCleanup.Phase.BlockedPeer;
            waiting = database.ObserveAsync(await session.Task, await reserved.Task, observation.Token);
            if (await Task.WhenAny(waiting, peer, winner).WaitAsync(token) == waiting)
            {
                await waiting;
                observed = true;
            }
            await NativeRaceCleanup.RequirePendingAsync(winner);
            phase = NativeRaceCleanup.Phase.ReleaseJoin;
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            NativeRaceCleanup.Report(phase, observed, winner, peer);
            await NativeRaceCleanup.CompleteAsync(release, observation, waiting, winner, peer, failure);
        }
        Assert.True(observed);
        return [await winner, await peer!];
    }

    /// <summary>Rejects an early completed invocation instead of waiting indefinitely for an unreachable lock signal.</summary>
    private static async Task RequireSignalAsync(Task signal, Task operation, CancellationToken token, Task? winner = null)
    {
        await Task.WhenAny(signal, operation, winner ?? operation).WaitAsync(token);
        if (winner is not null)
        {
            await NativeRaceCleanup.RequirePendingAsync(winner);
        }
        if (!signal.IsCompleted)
        {
            await operation;
            throw new InvalidOperationException();
        }
    }
}
