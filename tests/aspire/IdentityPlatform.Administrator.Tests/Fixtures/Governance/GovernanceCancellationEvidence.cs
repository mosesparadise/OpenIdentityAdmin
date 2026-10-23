using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Records only bounded native cancellation facts before a service sanitizes provider exceptions.</summary>
internal sealed class GovernanceCancellationEvidence : DbCommandInterceptor
{
    internal const string Roles = "AspNetRoles";
    internal const string Users = "AspNetUsers";
    internal const string Singleton = "AdministratorGovernance";
    private const int OperationSeconds = 5;
    private const string MissingCallback = "No matching native callback.";
    private const string CallbackFormat = "callback={0}; cancellationShape={1}; deadlineShape={2}; caller={3}; observedCanceled={4}; callbackCanceled={5}; budgetElapsed={6}";
    private static readonly CompositeFormat DiagnosticFormat = CompositeFormat.Parse(CallbackFormat);
    private enum CallbackKind
    {
        Failed, Canceled
    }
    private PlatformDbContext? owner;
    private string table = string.Empty;
    private bool sqlAllowed;
    private Func<bool> callerRequested = () => false;
    private Stopwatch? elapsed;
    private CancellationToken? expectedToken;
    internal CancellationToken ObservedToken
    {
        get; private set;
    }
    internal int Commands
    {
        get; private set;
    }
    internal bool NativeCallerCancellation
    {
        get; private set;
    }
    internal bool NativeDeadlineTermination
    {
        get; private set;
    }
    internal bool TokenMatched
    {
        get; private set;
    }
    internal string NativeFailureEvidence { get; private set; } = MissingCallback;

    /// <summary>Targets one exact context and table; an empty table selects native role/user statements for no-command ownership assertions.</summary>
    internal void Arm(PlatformDbContext context, string selectedTable, bool sql, Func<bool> requested,
        Stopwatch? watch = null, CancellationToken? token = null)
    {
        Assert.True(selectedTable is "" or Roles or Users or Singleton);
        owner = context;
        table = selectedTable;
        sqlAllowed = sql;
        callerRequested = requested;
        elapsed = watch;
        expectedToken = token;
        TokenMatched = true;
        Commands = 0;
    }

    /// <summary>Captures actual manager query dispatch and its forwarded cancellation token without retaining command payloads.</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Observe(command, eventData.Context, cancellationToken);
        return ValueTask.FromResult(result);
    }

    /// <summary>Captures actual native or singleton UPDATE dispatch using the same bounded metadata-only observation.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Observe(command, eventData.Context, cancellationToken);
        return ValueTask.FromResult(result);
    }

    /// <summary>Classifies the original native failure before production can replace it with a fixed unavailable outcome.</summary>
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (Matches(command, eventData.Context))
        {
            var facts = Snapshot(cancellationToken);
            NativeCallerCancellation |= CeremonyCancellation.IsCallerCancellation(eventData.Exception, sqlAllowed,
                facts.Caller && facts.ObservedCanceled);
            NativeDeadlineTermination |= GovernanceDeadlineEvidence.Accept(facts.Caller, facts.BudgetElapsed, facts.ObservedCanceled,
                CeremonyCancellation.IsCallerCancellation(eventData.Exception, sqlAllowed, true),
                CeremonyCancellation.IsProviderTimeout(eventData.Exception, sqlAllowed));
            Capture(CallbackKind.Failed, eventData.Exception, facts);
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires actual EF cancellation, original token cancellation and the independently owned caller/deadline condition.</summary>
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (Matches(command, eventData.Context))
        {
            var facts = Snapshot(cancellationToken);
            if (facts.CallbackCanceled && facts.ObservedCanceled)
            {
                NativeCallerCancellation |= facts.Caller;
                NativeDeadlineTermination |= facts.DeadlineExpired;
            }
            Capture(CallbackKind.Canceled, null, facts);
        }
        return Task.CompletedTask;
    }

    /// <summary>Snapshots finite classifier and token facts at the original callback, never after cleanup cancellation.</summary>
    private void Capture(CallbackKind callback, Exception? failure, CallbackFacts facts)
    {
        var cancellationShape = CeremonyCancellation.IsCallerCancellation(failure, sqlAllowed, true);
        var deadlineShape = CeremonyCancellation.IsDeadlineFailure(failure, sqlAllowed, true);
        NativeFailureEvidence = string.Format(CultureInfo.InvariantCulture, DiagnosticFormat, callback, cancellationShape, deadlineShape,
            facts.Caller, facts.ObservedCanceled, facts.CallbackCanceled, facts.BudgetElapsed);
    }

    /// <summary>Samples callback-entry facts once so diagnostic work cannot create deadline acceptance.</summary>
    private CallbackFacts Snapshot(CancellationToken token) => new(callerRequested(), ObservedToken.IsCancellationRequested,
        token.IsCancellationRequested, elapsed is { } watch && watch.Elapsed >= TimeSpan.FromSeconds(OperationSeconds));

    private readonly record struct CallbackFacts(bool Caller, bool ObservedCanceled, bool CallbackCanceled, bool BudgetElapsed)
    {
        internal bool DeadlineExpired => !Caller && ObservedCanceled && BudgetElapsed;
    }

    /// <summary>Captures no SQL, parameters, error text or exception object; token equality remains independent of owner validation.</summary>
    private void Observe(DbCommand command, Microsoft.EntityFrameworkCore.DbContext? context, CancellationToken token)
    {
        if (Matches(command, context))
        {
            Commands++;
            ObservedToken = token;
            TokenMatched &= token.CanBeCanceled && (expectedToken is null || token == expectedToken.Value);
        }
    }

    /// <summary>Filters by real context identity and a closed table catalogue, excluding unrelated startup/account statements.</summary>
    private bool Matches(DbCommand command, Microsoft.EntityFrameworkCore.DbContext? context) => owner is not null &&
        ReferenceEquals(owner, context) && (table.Length == 0
            ? command.CommandText.Contains(Roles, StringComparison.Ordinal) || command.CommandText.Contains(Users, StringComparison.Ordinal)
            : command.CommandText.Contains(table, StringComparison.Ordinal));
}
