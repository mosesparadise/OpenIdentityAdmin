using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

public sealed partial class BootstrapTests
{
    private static readonly string[] Races = ["identical", "conflicting"];
    private static readonly string[] Failures = ["audit", "manager", "cancel-lock", "kill-precommit", "cancel-manager"];
    public static IEnumerable<TheoryDataRow<string, string>> RaceCases => Cases(Races);
    public static IEnumerable<TheoryDataRow<string, string>> FailureCases => Cases(Failures);

    [Theory]
    [MemberData(nameof(RaceCases))]
    public async Task Independent_processes_contend_and_only_one_initializes(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var secondOperation = scenario == "identical" ? lease.OperationId : Guid.NewGuid();
        await using var first = new BootstrapProcess(provider, lease.RuntimeConnection);
        await using var second = new BootstrapProcess(provider, lease.RuntimeConnection);
        await using (var held = await GovernanceLock.AcquireAsync(lease))
        {
            await first.SendAsync(lease.Input());
            await second.SendAsync(lease.Input(operation: secondOperation));
            await held.ObserveWaitersAsync(lease, 2);
        }
        var results = new[] { await first.CompleteAsync(), await second.CompleteAsync() };
        Assert.True(results.All(result => !result.Output.Contains(lease.Password, StringComparison.Ordinal)), "Bootstrap output disclosed a credential.");
        Assert.Equal(1, results.Count(result => result.ExitCode == 0 && result.Output.Contains("enrollment initialized", StringComparison.Ordinal)));
        if (scenario == "identical")
        {
            Assert.True(results.All(result => result.ExitCode == 0), "Identical operation did not reconcile.");
            Assert.Equal(1, results.Count(result => result.Output.Contains("already initialized", StringComparison.Ordinal)));
        }
        else
        {
            Assert.Equal(1, results.Count(result => result.ExitCode == 1));
        }
        var winner = results[0].Output.Contains("enrollment initialized", StringComparison.Ordinal) ? lease.OperationId : secondOperation;
        await AssertCreatedAsync(lease, lease.Password, winner);
        var before = await SnapshotAsync(lease);
        var replay = await RunAsync(lease, provider, lease.Input(operation: winner));
        Assert.True(replay.ExitCode == 0 && replay.Output.Contains("already initialized", StringComparison.Ordinal), "Winner reconciliation failed.");
        Assert.True(before == await SnapshotAsync(lease), "Winner reconciliation changed persisted state.");
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Failed_or_interrupted_initialization_rolls_back_every_write(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        string? cancellationFailure = null;
        switch (scenario)
        {
            case "audit":
                await lease.AuditPermissionAsync(false, Token);
                try
                {
                    var failure = await Record.ExceptionAsync(() => lease.InitializeAsync(Token));
                    Assert.True(failure is DbUpdateException && (failure.InnerException is Npgsql.PostgresException { SqlState: "42501" } ||
                        failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 229 }), "Expected audit INSERT permission denial.");
                }
                finally
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await lease.AuditPermissionAsync(true, cleanup.Token);
                }
                break;
            case "manager":
                await using (var rejected = lease.WithRejectedUsers())
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => lease.InitializeAsync(Token, rejected));
                }
                break;
            case "cancel-lock":
                RequireCancellationClassifierControls();
                using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token))
                await using (var held = await GovernanceLock.AcquireAsync(lease))
                {
                    var attempt = lease.InitializeAsync(cancelled.Token);
                    try
                    {
                        await held.ObserveWaitersAsync(lease, 1);
                        Assert.False(attempt.IsCompleted, "Operation was not blocked at the held governance row.");
                    }
                    finally
                    {
                        cancellationFailure = await CancelAndDrainAsync(attempt, cancelled, sqlAttention: provider == "SqlServer");
                    }
                }
                break;
            case "cancel-manager":
                var gate = new ManagerCancellationGate();
                await using (var services = lease.WithInterceptor(gate))
                using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token))
                {
                    var attempt = lease.InitializeAsync(cancelled.Token, services);
                    try
                    {
                        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Token);
                        Assert.False(attempt.IsCompleted, "Native manager INSERT did not remain pending.");
                    }
                    finally
                    {
                        cancellationFailure = await CancelAndDrainAsync(attempt, cancelled);
                    }
                }
                break;
            case "kill-precommit":
                await using (var process = new BootstrapProcess(provider, lease.RuntimeConnection))
                await using (var held = await GovernanceLock.AcquireAsync(lease, audit: true))
                {
                    await process.SendAsync(lease.Input());
                    await held.ObserveWaitersAsync(lease, 1);
                    process.Terminate();
                    var result = await process.CompleteAsync();
                    Assert.NotEqual(0, result.ExitCode);
                }
                break;
        }
        await lease.AssertRuntimeDrainedAsync();
        Assert.True(before == await SnapshotAsync(lease), "Failed initialization left partial state.");
        Assert.Equal(BootstrapOutcome.Created, await lease.InitializeAsync(Token));
        Assert.True(cancellationFailure is null, "Cancellation classification failed after drain/rollback/recovery proof: " + cancellationFailure);
    }
    private static async Task<string?> CancelAndDrainAsync(Task attempt, CancellationTokenSource operation, bool sqlAttention = false)
    {
        await operation.CancelAsync();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var failure = await Record.ExceptionAsync(() => attempt.WaitAsync(cleanup.Token));
        // Preserve only the exact observed/source-supported nonfatal attention shapes.
        // An arbitrary companion error cannot establish cancellation.
        var attention = failure is Microsoft.Data.SqlClient.SqlException sql &&
            IsSqlCancellation(sqlAttention, operation.IsCancellationRequested,
                sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Select(error => new SqlCancellationError(
                    error.Number, error.Class, error.State, error.LineNumber, string.IsNullOrEmpty(error.Procedure), Meaning(error.Message))).ToArray(),
                hasInnerException: sql.InnerException is not null);
        var diagnostic = failure is Microsoft.Data.SqlClient.SqlException providerFailure
            ? "SqlException inner=" + (providerFailure.InnerException is not null) + " errors=" + string.Join(";",
                providerFailure.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Select(error =>
                    $"number={error.Number},class={error.Class},state={error.State},line={error.LineNumber},procedureEmpty={string.IsNullOrEmpty(error.Procedure)},attention={error.Message == "Operation cancelled by user."},statementTerminated={error.Message == "The statement has been terminated."},severe={error.Message == "A severe error occurred on the current command.  The results, if any, should be discarded."}"))
            : failure?.GetType().Name ?? "no failure";
        Assert.True(attempt.IsCompleted && !cleanup.IsCancellationRequested,
            "Original operation did not finish within independent cleanup deadline: " + diagnostic);
        return failure is OperationCanceledException || attention ? null : diagnostic;
    }

    private enum SqlErrorMeaning
    {
        Unknown, Attention, Severe, StatementTerminated
    }
    private readonly record struct SqlCancellationError(int Number, byte Class, byte State, int Line, bool EmptyProcedure, SqlErrorMeaning Meaning);
    private static SqlErrorMeaning Meaning(string message) => message switch
    {
        "Operation cancelled by user." => SqlErrorMeaning.Attention,
        "A severe error occurred on the current command.  The results, if any, should be discarded." => SqlErrorMeaning.Severe,
        "The statement has been terminated." => SqlErrorMeaning.StatementTerminated,
        _ => SqlErrorMeaning.Unknown
    };
    private static readonly SqlCancellationError Attention = new(0, 11, 0, 0, true, SqlErrorMeaning.Attention);
    private static readonly SqlCancellationError[] InterruptedStatement =
    [
        new(0, 11, 0, 0, true, SqlErrorMeaning.Severe),
        Attention,
        new(3621, 0, 0, 1, true, SqlErrorMeaning.StatementTerminated)
    ];
    private static bool IsSqlCancellation(bool sqlLockCase, bool cancelled, IReadOnlyList<SqlCancellationError> errors, bool hasInnerException = false) =>
        sqlLockCase && cancelled && !hasInnerException && (errors.Count == 1 && errors[0] == Attention || errors.SequenceEqual(InterruptedStatement));

    private static void RequireCancellationClassifierControls()
    {
        Assert.True(IsSqlCancellation(true, true, [Attention]), "Native attention must be recognized.");
        Assert.True(IsSqlCancellation(true, true, InterruptedStatement), "Observed interrupted statement must be recognized.");
        Assert.False(IsSqlCancellation(false, true, InterruptedStatement), "Attention escaped the SQL lock-case boundary.");
        Assert.False(IsSqlCancellation(true, false, InterruptedStatement), "Attention without explicit cancellation was accepted.");
        Assert.False(IsSqlCancellation(true, true, []), "Empty provider errors were accepted.");
        Assert.False(IsSqlCancellation(true, true, [.. InterruptedStatement, Attention]), "Extra provider errors were accepted.");
        Assert.False(IsSqlCancellation(true, true, InterruptedStatement.Reverse().ToArray()), "Wrong error order was accepted.");
        foreach (var replacement in new[]
        {
            InterruptedStatement[2] with { Meaning = SqlErrorMeaning.Unknown },
            InterruptedStatement[2] with { Number = -2 },
            InterruptedStatement[2] with { Number = 1205 },
            InterruptedStatement[2] with { Number = 229 },
            InterruptedStatement[2] with { Class = 20 },
            InterruptedStatement[2] with { State = 1 },
            InterruptedStatement[2] with { Line = 2 },
            InterruptedStatement[2] with { EmptyProcedure = false }
        })
        {
            Assert.False(IsSqlCancellation(true, true, [InterruptedStatement[0], Attention, replacement]),
                "An unrelated companion error was accepted as cancellation.");
        }
        Assert.False(IsSqlCancellation(true, true, InterruptedStatement, hasInnerException: true), "An inner exception was accepted.");
        Assert.False(IsSqlCancellation(true, true, [Attention, Attention]), "Duplicated attention was accepted.");
        for (var index = 0; index < InterruptedStatement.Length; index++)
        {
            Assert.False(IsSqlCancellation(true, true, InterruptedStatement.Where((_, position) => position != index).ToArray()),
                "An omitted error was accepted.");
            var original = InterruptedStatement[index];
            foreach (var changed in new[]
            {
                original with { Number = original.Number + 1 },
                original with { Class = (byte)(original.Class + 1) },
                original with { State = 1 },
                original with { Line = original.Line + 1 },
                original with { EmptyProcedure = false },
                original with { Meaning = SqlErrorMeaning.Unknown }
            })
            {
                var errors = InterruptedStatement.ToArray();
                errors[index] = changed;
                Assert.False(IsSqlCancellation(true, true, errors), "A changed error field or message was accepted.");
            }
        }
        Assert.Equal(SqlErrorMeaning.Unknown, Meaning("synthetic-unrecognized-provider-error"));
    }

}
