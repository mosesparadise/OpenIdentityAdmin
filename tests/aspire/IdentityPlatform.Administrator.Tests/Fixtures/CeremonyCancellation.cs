using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Recognizes only the provider cancellation shapes already established by bootstrap evidence.</summary>
internal static class CeremonyCancellation
{
    private const int DrainSeconds = 40;
    private const string AttentionMessage = "Operation cancelled by user.";
    private const string SevereMessage = "A severe error occurred on the current command.  The results, if any, should be discarded.";
    private const string TerminatedMessage = "The statement has been terminated.";
    private const string NoFailure = "no-failure";
    private static readonly Error Attention = new(0, 11, 0, 0, true, AttentionMessage);
    private static readonly Error[] Interrupted = [new(0, 11, 0, 0, true, SevereMessage), Attention, new(3621, 0, 0, 1, true, TerminatedMessage)];
    private readonly record struct Error(int Number, byte Class, byte State, int Line, bool EmptyProcedure, string Message);

    /// <summary>Cancels and independently drains an operation without accepting unrelated SQL errors or retaining their messages.</summary>
    internal static async Task<string?> CancelAndDrainAsync(Task task, CancellationTokenSource operation, bool sqlAllowed)
    {
        RequireControls();
        await operation.CancelAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        var failure = await Record.ExceptionAsync(() => task.WaitAsync(deadline.Token));
        Assert.True(task.IsCompleted && !deadline.IsCancellationRequested);
        var accepted = failure is SqlException sql && sqlAllowed && operation.IsCancellationRequested &&
            sql.InnerException is null && Matches(sql.Errors.Cast<SqlError>().Select(error =>
                new Error(error.Number, error.Class, error.State, error.LineNumber, string.IsNullOrEmpty(error.Procedure), error.Message)).ToArray());
        return failure is OperationCanceledException || accepted ? null : failure?.GetType().Name ?? NoFailure;
    }

    /// <summary>Classifies an observed native caller cancellation before a service replaces its exception with a safe boundary outcome.</summary>
    internal static bool IsCallerCancellation(Exception? failure, bool sqlAllowed, bool requested)
    {
        RequireControls();
        return requested && (failure is OperationCanceledException || failure is SqlException sql && sqlAllowed &&
            sql.InnerException is null && Matches(sql.Errors.Cast<SqlError>().Select(error =>
                new Error(error.Number, error.Class, error.State, error.LineNumber, string.IsNullOrEmpty(error.Procedure), error.Message)).ToArray()));
    }

    /// <summary>Accepts only documented cancellation or timeout shapes after the observed production deadline, never unrelated SQL failures.</summary>
    internal static void RequireDeadlineFailure(Exception? failure, bool sqlAllowed)
    {
        RequireControls();
        RequireTimeoutControls();
        if (failure is OperationCanceledException)
        {
            return;
        }
        Assert.True(sqlAllowed && failure is SqlException, failure?.GetType().Name ?? NoFailure);
        var sql = (SqlException)failure!;
        var errors = sql.Errors.Cast<SqlError>().ToArray();
        var cancellation = sql.InnerException is null && Matches(errors.Select(error =>
            new Error(error.Number, error.Class, error.State, error.LineNumber, string.IsNullOrEmpty(error.Procedure), error.Message)).ToArray());
        var timeout = MatchesTimeout(errors.Select(error => (error.Number, error.Class)).ToArray(), sql.InnerException);
        const string Diagnostic = "Unexpected deadline metadata: ";
        const string Separator = ",";
        const string Field = ":";
        var metadata = string.Join(Separator, errors.Select(error => string.Join(Field, error.Number, error.Class, error.State, error.LineNumber)));
        Assert.True(cancellation || timeout, Diagnostic + metadata);
    }

    /// <summary>Recognizes one provider timeout with its documented class and required native command-timeout cause.</summary>
    private static bool MatchesTimeout((int Number, byte Class)[] errors, Exception? inner)
    {
        const int CommandTimeout = -2;
        const int LockTimeout = 1222;
        const int WaitTimeout = 258;
        const byte TimeoutClass = 11;
        const byte LockClass = 16;
        return errors.Length == 1 && (errors[0] == (CommandTimeout, TimeoutClass) && inner is Win32Exception { NativeErrorCode: WaitTimeout } ||
            errors[0] == (LockTimeout, LockClass) && inner is null);
    }

    /// <summary>Proves companion errors, wrong causes/classes and unrelated provider failures never count as deadline evidence.</summary>
    private static void RequireTimeoutControls()
    {
        const int WaitTimeout = 258;
        const int CommandNumber = -2;
        const byte CommandClass = 11;
        const int LockNumber = 1222;
        const byte LockClass = 16;
        (int Number, byte Class)[] command = [(CommandNumber, CommandClass)];
        (int Number, byte Class)[] locked = [(LockNumber, LockClass)];
        var cause = new Win32Exception(WaitTimeout);
        Assert.True(MatchesTimeout(command, cause));
        Assert.True(MatchesTimeout(locked, null));
        Assert.False(MatchesTimeout(command, null));
        Assert.False(MatchesTimeout(locked, cause));
        Assert.False(MatchesTimeout([], cause));
        Assert.False(MatchesTimeout([.. command, .. locked], cause));
        Assert.False(MatchesTimeout([(command[0].Number, locked[0].Class)], cause));
        Assert.False(MatchesTimeout([(locked[0].Number, command[0].Class)], null));
        Assert.False(MatchesTimeout([(command[0].Number - 1, command[0].Class)], cause));
        Assert.False(MatchesTimeout(command, new Win32Exception(WaitTimeout + 1)));
    }

    /// <summary>Requires the exact ordered metadata/message collection; arbitrary companion failures never count as cancellation.</summary>
    private static bool Matches(IReadOnlyList<Error> errors) => errors.Count == 1 && errors[0] == Attention || errors.SequenceEqual(Interrupted);

    /// <summary>Checks positive and substituted/omitted/extra-error controls whenever the provider classifier is exercised.</summary>
    private static void RequireControls()
    {
        Assert.True(Matches([Attention]));
        Assert.True(Matches(Interrupted));
        Assert.False(Matches([]));
        Assert.False(Matches([.. Interrupted, Attention]));
        Assert.False(Matches(Interrupted.Reverse().ToArray()));
        foreach (var original in Interrupted)
        {
            var changed = Interrupted.Select(error => error == original ? error with { Number = error.Number + 1 } : error).ToArray();
            Assert.False(Matches(changed));
            Assert.False(Matches(Interrupted.Where(error => error != original).ToArray()));
        }
    }
    /// <summary>Classifies native deadline evidence only after the observed production token expired, without retaining exception diagnostics.</summary>
    internal static bool IsDeadlineFailure(Exception? failure, bool sqlAllowed, bool expired)
    {
        RequireControls();
        RequireTimeoutControls();
        if (!expired)
        {
            return false;
        }
        if (failure is OperationCanceledException)
        {
            return true;
        }
        if (!sqlAllowed || failure is not SqlException sql)
        {
            return false;
        }
        var errors = sql.Errors.Cast<SqlError>().ToArray();
        return sql.InnerException is null && Matches(errors.Select(error =>
            new Error(error.Number, error.Class, error.State, error.LineNumber, string.IsNullOrEmpty(error.Procedure), error.Message)).ToArray()) ||
            MatchesTimeout(errors.Select(error => (error.Number, error.Class)).ToArray(), sql.InnerException);
    }
}
