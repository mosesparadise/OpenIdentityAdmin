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
}
