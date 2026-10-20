using System.Data.Common;
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
    internal static Task<NativeClientHttpResult[]> InvokeAsync(BootstrapSessions database, NativeClientHttp first, NativeClientHttp second,
        NativeClientCallbackRace winnerProbe, NativeClientCallbackRace peerProbe, Uri callback, string cookie)
    {
        var reserved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        winnerProbe.Locked = reserved;
        winnerProbe.Release = release.Task;
        return NativeBlockedRace.InvokeAsync(() => first.InvokeAsync(callback, cookie), reserved.Task, () =>
        {
            peerProbe.BeforeLock = session;
            return second.InvokeAsync(callback, cookie);
        }, session.Task, release, database.ObserveAsync);
    }
}
