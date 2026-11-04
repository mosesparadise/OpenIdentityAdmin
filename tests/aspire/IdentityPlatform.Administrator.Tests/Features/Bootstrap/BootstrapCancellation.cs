namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Narrow provider cancellation classifier, safe diagnostics and independently seeded controls.</summary>
internal static class BootstrapCancellation
{
    private const int DrainSeconds = 40;
    private const string InnerExceptionLabel = "SqlException inner=";
    private const string ErrorsLabel = " errors=";
    private const string ErrorSeparator = ";";
    private const string NoFailure = "no failure";
    private const string DrainDeadlineFailure = "Original operation did not finish within independent cleanup deadline: ";
    private const string AttentionMessage = "Operation cancelled by user.";
    private const string SevereMessage = "A severe error occurred on the current command.  The results, if any, should be discarded.";
    private const string TerminatedMessage = "The statement has been terminated.";
    private const int AttentionErrorClass = 11;
    private const int StatementTerminatedError = 3621;
    private const string UnrecognizedNativeAttention = "Native attention must be recognized.";
    private const string UnrecognizedInterruptedStatement = "Observed interrupted statement must be recognized.";
    private const string CancellationEscapedLockCase = "Attention escaped the SQL lock-case boundary.";
    private const string MissingExplicitCancellation = "Attention without explicit cancellation was accepted.";
    private const string AcceptedEmptyErrors = "Empty provider errors were accepted.";
    private const string AcceptedExtraErrors = "Extra provider errors were accepted.";
    private const string AcceptedWrongErrorOrder = "Wrong error order was accepted.";
    private const string AcceptedInnerException = "An inner exception was accepted.";
    private const string AcceptedDuplicateAttention = "Duplicated attention was accepted.";
    private const string UnrecognizedProviderMessage = "synthetic-unrecognized-provider-error";
    private const int CompanionIndex = 2;
    private const int TimeoutError = -2;
    private const int UnexpectedErrorLine = 2;
    private const int DeadlockError = 1205;
    private const int PermissionDeniedError = 229;
    private const int FatalErrorClass = 20;
    private const string AcceptedUnrelatedCompanion = "An unrelated companion error was accepted as cancellation.";
    private const string AcceptedOmittedError = "An omitted error was accepted.";
    private const string AcceptedChangedError = "A changed error field or message was accepted.";

    /// <summary>Cancels explicitly and joins the original operation within its independent cleanup deadline.</summary>
    internal static async Task<string?> CancelAndDrainAsync(Task attempt, CancellationTokenSource operation, bool sqlAttention = false)
    {
        await operation.CancelAsync();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        var failure = await Record.ExceptionAsync(() => attempt.WaitAsync(cleanup.Token));
        // Preserve only the exact observed/source-supported nonfatal attention shapes.
        // An arbitrary companion error cannot establish cancellation.
        var attention = failure is Microsoft.Data.SqlClient.SqlException sql &&
            IsSqlCancellation(sqlAttention, operation.IsCancellationRequested,
                sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Select(error => new SqlCancellationError(
                    error.Number, error.Class, error.State, error.LineNumber, string.IsNullOrEmpty(error.Procedure), Meaning(error.Message))).ToArray(),
                hasInnerException: sql.InnerException is not null);
        var diagnostic = failure is Microsoft.Data.SqlClient.SqlException providerFailure
            ? InnerExceptionLabel + (providerFailure.InnerException is not null) + ErrorsLabel + string.Join(ErrorSeparator,
                providerFailure.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Select(error =>
                    NumberLabel + error.Number + ClassLabel + error.Class + StateLabel + error.State + LineLabel + error.LineNumber +
                    ProcedureLabel + string.IsNullOrEmpty(error.Procedure) + AttentionLabel + (error.Message == AttentionMessage) +
                    TerminatedLabel + (error.Message == TerminatedMessage) + SevereLabel + (error.Message == SevereMessage)))
            : failure?.GetType().Name ?? NoFailure;
        Assert.True(attempt.IsCompleted && !cleanup.IsCancellationRequested,
            DrainDeadlineFailure + diagnostic);
        return failure is OperationCanceledException || attention ? null : diagnostic;
    }

    private enum SqlErrorMeaning
    {
        Unknown, Attention, Severe, StatementTerminated
    }
    private readonly record struct SqlCancellationError(int Number, byte Class, byte State, int Line, bool EmptyProcedure, SqlErrorMeaning Meaning);
    /// <summary>Maps only the exact supported provider messages to finite error meanings.</summary>
    private static SqlErrorMeaning Meaning(string message) => message switch
    {
        AttentionMessage => SqlErrorMeaning.Attention,
        SevereMessage => SqlErrorMeaning.Severe,
        TerminatedMessage => SqlErrorMeaning.StatementTerminated,
        _ => SqlErrorMeaning.Unknown
    };
    private static readonly SqlCancellationError Attention = new(0, 11, 0, 0, true, SqlErrorMeaning.Attention);
    private static readonly SqlCancellationError[] InterruptedStatement =
    [
        new(0, AttentionErrorClass, 0, 0, true, SqlErrorMeaning.Severe),
        Attention,
        new(StatementTerminatedError, 0, 0, 1, true, SqlErrorMeaning.StatementTerminated)
    ];
    /// <summary>Accepts only explicit SQL lock cancellation with the exact ordered nonfatal provider errors.</summary>
    private static bool IsSqlCancellation(bool sqlLockCase, bool cancelled, IReadOnlyList<SqlCancellationError> errors, bool hasInnerException = false) =>
        sqlLockCase && cancelled && !hasInnerException && (errors.Count == 1 && errors[0] == Attention || errors.SequenceEqual(InterruptedStatement));

    private const string NumberLabel = "number=";
    private const string ClassLabel = ",class=";
    private const string StateLabel = ",state=";
    private const string LineLabel = ",line=";
    private const string ProcedureLabel = ",procedureEmpty=";
    private const string AttentionLabel = ",attention=";
    private const string TerminatedLabel = ",statementTerminated=";
    private const string SevereLabel = ",severe=";

    /// <summary>Runs every original positive and negative classifier control in its original order.</summary>
    internal static void RequireCancellationClassifierControls()
    {
        Assert.True(IsSqlCancellation(true, true, [Attention]), UnrecognizedNativeAttention);
        Assert.True(IsSqlCancellation(true, true, InterruptedStatement), UnrecognizedInterruptedStatement);
        Assert.False(IsSqlCancellation(false, true, InterruptedStatement), CancellationEscapedLockCase);
        Assert.False(IsSqlCancellation(true, false, InterruptedStatement), MissingExplicitCancellation);
        Assert.False(IsSqlCancellation(true, true, []), AcceptedEmptyErrors);
        Assert.False(IsSqlCancellation(true, true, [.. InterruptedStatement, Attention]), AcceptedExtraErrors);
        Assert.False(IsSqlCancellation(true, true, InterruptedStatement.Reverse().ToArray()), AcceptedWrongErrorOrder);
        RequireCompanionControls();
        Assert.False(IsSqlCancellation(true, true, InterruptedStatement, hasInnerException: true), AcceptedInnerException);
        Assert.False(IsSqlCancellation(true, true, [Attention, Attention]), AcceptedDuplicateAttention);
        RequireFieldControls();
        Assert.Equal(SqlErrorMeaning.Unknown, Meaning(UnrecognizedProviderMessage));
    }

    /// <summary>Rejects unrelated companion codes, severities, states, lines and procedures.</summary>
    private static void RequireCompanionControls()
    {
        foreach (var replacement in new[]
        {
            InterruptedStatement[CompanionIndex] with { Meaning = SqlErrorMeaning.Unknown },
            InterruptedStatement[CompanionIndex] with { Number = TimeoutError },
            InterruptedStatement[CompanionIndex] with { Number = DeadlockError },
            InterruptedStatement[CompanionIndex] with { Number = PermissionDeniedError },
            InterruptedStatement[CompanionIndex] with { Class = FatalErrorClass },
            InterruptedStatement[CompanionIndex] with { State = 1 },
            InterruptedStatement[CompanionIndex] with { Line = UnexpectedErrorLine },
            InterruptedStatement[CompanionIndex] with { EmptyProcedure = false }
        })
        {
            Assert.False(IsSqlCancellation(true, true, [InterruptedStatement[0], Attention, replacement]),
                AcceptedUnrelatedCompanion);
        }
    }

    /// <summary>Rejects omitted or changed fields at every position of the exact provider error sequence.</summary>
    private static void RequireFieldControls()
    {
        for (var index = 0; index < InterruptedStatement.Length; index++)
        {
            Assert.False(IsSqlCancellation(true, true, InterruptedStatement.Where((_, position) => position != index).ToArray()),
                AcceptedOmittedError);
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
                Assert.False(IsSqlCancellation(true, true, errors), AcceptedChangedError);
            }
        }
    }
}
