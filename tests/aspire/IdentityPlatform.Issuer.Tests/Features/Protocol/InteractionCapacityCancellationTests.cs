using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class InteractionCapacityTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public void Cancellation_oracle_rejects_timeouts_unrelated_errors_and_missing_cancellation(string provider)
    {
        Assert.True(IsExpectedCancellation(provider, true, new OperationCanceledException()));
        Assert.False(IsExpectedCancellation(provider, false, new OperationCanceledException()));
        Assert.False(IsExpectedCancellation(provider, true, null));
        Assert.False(IsExpectedCancellation(provider, true, new InvalidOperationException("synthetic")));
        var attention = new AttentionError(1, 0, 0, 11, "", 0, "Operation cancelled by user.", false);
        Assert.Equal(provider == "SqlServer", IsAttentionCancellation(provider, true, attention));
        Assert.False(IsAttentionCancellation(provider, false, attention));
        AttentionError[] rejected =
        [
            attention with { Count = 0 }, attention with { Count = 2 },
            attention with { Number = -2 }, attention with { Number = 1222 }, attention with { Number = 1205 },
            attention with { State = 1 }, attention with { Class = 20 },
            attention with { Procedure = "synthetic" }, attention with { Line = 1 },
            attention with { Message = "A severe error occurred on the current command." },
            attention with { Message = "Operation cancelled by user. unexpected suffix" },
            attention with { HasInner = true }
        ];
        foreach (var error in rejected)
        {
            Assert.False(IsAttentionCancellation(provider, true, error), "Unrelated provider failure was accepted as cancellation.");
        }
    }

    private sealed record AttentionError(int Count, int Number, byte State, byte Class, string Procedure, int Line, string Message, bool HasInner);

    private static bool IsExpectedCancellation(string provider, bool requested, Exception? failure)
    {
        if (!requested)
        {
            return false;
        }
        if (failure is OperationCanceledException)
        {
            return true;
        }
        if (failure is not SqlException { Errors.Count: 1 } sql)
        {
            return false;
        }
        var error = sql.Errors[0];
        return IsAttentionCancellation(provider, requested,
            new(1, error.Number, error.State, error.Class, error.Procedure, error.LineNumber, error.Message, sql.InnerException is not null));
    }

    // Pinned SqlClient 6.1.6 attention acknowledgement: TdsParser.cs lines 2673-2696.
    // Number zero alone also describes unrelated errors. Compare the resource privately.
    private static bool IsAttentionCancellation(string provider, bool requested, AttentionError error) =>
        provider == "SqlServer" && requested && error is { Count: 1, Number: 0, State: 0, Class: 11, Procedure: "", Line: 0, HasInner: false } &&
        error.Message == "Operation cancelled by user.";

    private static string CancellationDiagnostic(Exception? failure) => failure switch
    {
        null => "Blocked admission returned successfully after cancellation.",
        OperationCanceledException => "Cancellation outcome had no requested caller cancellation.",
        SqlException { Errors.Count: > 0 } sql => $"Unexpected cancellation outcome: sql count={sql.Errors.Count}, inner={sql.InnerException is not null}; " +
            string.Join("; ", sql.Errors.Cast<SqlError>().Take(8).Select(error =>
                $"number={error.Number}, state={error.State}, class={error.Class}, line={error.LineNumber}, emptyProcedure={string.IsNullOrEmpty(error.Procedure)}, kind={ErrorCategory(error.Message)}")),
        SqlException => "Unexpected cancellation outcome: sql with no errors.",
        _ => "Unexpected cancellation outcome: other exception category."
    };
    private static string ErrorCategory(string message) => message switch
    {
        "Operation cancelled by user." => "attention",
        "A severe error occurred on the current command.  The results, if any, should be discarded." => "severe",
        "The statement has been terminated." => "statement-terminated",
        _ => "other"
    };
}
