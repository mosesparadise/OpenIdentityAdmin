using System.Data.Common;
using System.Diagnostics;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Captures the exact pending gate command and its native termination without retaining SQL, parameters or exception text.</summary>
internal sealed class PilotWaitProbe : DbCommandInterceptor
{
    private const string Gate = "PilotProtocolGate";
    private const string Update = "UPDATE";
    private Guid? commandId;
    private CancellationToken dispatchedToken;
    private const int TransactionSeconds = 5;
    internal long StartedAt
    {
        get; set;
    }
    internal Func<bool> CallerRequested { get; set; } = () => false;
    internal bool CallerCancellation
    {
        get; private set;
    }
    internal bool DeadlineTermination
    {
        get; private set;
    }
    internal bool Armed
    {
        get; set;
    }
    internal TaskCompletionSource<int> Session { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Adds a passive observer to the genuine issuer provider pipeline.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Records the first actual gate acquisition attempt after explicit arming; dispatch alone never proves a lock wait.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && commandId is null && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Gate, StringComparison.Ordinal))
        {
            Assert.NotNull(command.Transaction);
            commandId = eventData.CommandId;
            dispatchedToken = cancellationToken;
            Session.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Signals only failure of that exact provider command, allowing cancellation tests to join server work before releasing the holder.</summary>
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.CommandId == commandId)
        {
            var caller = CallerRequested();
            var canceled = dispatchedToken.IsCancellationRequested;
            var elapsed = Stopwatch.GetElapsedTime(StartedAt) >= TimeSpan.FromSeconds(TransactionSeconds);
            var sql = command.Connection is Microsoft.Data.SqlClient.SqlConnection;
            var shape = CeremonyCancellation.IsCallerCancellation(eventData.Exception, sql, requested: true);
            CallerCancellation = caller && canceled && shape;
            DeadlineTermination = GovernanceDeadlineEvidence.Accept(caller, elapsed, canceled, shape,
                CeremonyCancellation.IsProviderTimeout(eventData.Exception, sql));
            Failed.TrySetResult();
        }
        return Task.CompletedTask;
    }

    /// <summary>EF reports native cancellation separately from provider errors; both terminate the exact blocked command.</summary>
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.CommandId == commandId)
        {
            var caller = CallerRequested();
            var canceled = dispatchedToken.IsCancellationRequested && cancellationToken.IsCancellationRequested;
            var elapsed = Stopwatch.GetElapsedTime(StartedAt) >= TimeSpan.FromSeconds(TransactionSeconds);
            CallerCancellation = caller && canceled;
            DeadlineTermination = GovernanceDeadlineEvidence.Accept(caller, elapsed, canceled, cancellationShape: true, providerTimeout: false);
            Failed.TrySetResult();
        }
        return Task.CompletedTask;
    }
}
