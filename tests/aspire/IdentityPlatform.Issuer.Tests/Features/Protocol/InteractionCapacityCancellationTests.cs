using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class InteractionCapacityTests
{
    private sealed record CancellationError(int Number, byte State, byte Class, string Procedure, int Line, string Message);
    private static readonly CancellationError Attention = new(0, 0, 11, "", 0, "Operation cancelled by user.");
    private static readonly CancellationError Severe = new(0, 0, 11, "", 0, "A severe error occurred on the current command.  The results, if any, should be discarded.");
    private static readonly CancellationError Terminated = new(3621, 0, 0, "", 1, "The statement has been terminated.");

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public void Cancellation_oracle_rejects_timeouts_unrelated_errors_and_missing_cancellation(string provider)
    {
        Assert.True(IsExpectedCancellation(provider, true, new OperationCanceledException()));
        Assert.False(IsExpectedCancellation(provider, false, new OperationCanceledException()));
        Assert.False(IsExpectedCancellation(provider, true, null));
        Assert.False(IsExpectedCancellation(provider, true, new InvalidOperationException("synthetic")));
        CancellationError[][] accepted = [[Attention], [Severe, Attention, Terminated]];
        foreach (var errors in accepted)
        {
            Assert.Equal(provider == "SqlServer", IsAttentionCancellation(provider, true, false, errors));
            Assert.False(IsAttentionCancellation(provider, false, false, errors));
            Assert.False(IsAttentionCancellation(provider, true, true, errors));
            for (var index = 0; index < errors.Length; index++)
            {
                var original = errors[index];
                CancellationError[] altered =
                [
                    original with { Number = -2 }, original with { Number = 1222 }, original with { Number = 1205 },
                    original with { Number = original.Number + 1 }, original with { State = 1 }, original with { Class = 20 },
                    original with { Procedure = "synthetic" }, original with { Line = original.Line + 1 },
                    original with { Message = "Unrelated synthetic failure." }, original with { Message = original.Message + " unexpected suffix" }
                ];
                foreach (var error in altered)
                {
                    CancellationError[] candidate = [.. errors];
                    candidate[index] = error;
                    Assert.False(IsAttentionCancellation(provider, true, false, candidate), "Unrelated provider failure was accepted as cancellation.");
                }
            }
        }
        CancellationError[][] rejected =
        [
            [], [Severe], [Terminated], [Attention, Attention], [Severe, Attention], [Attention, Terminated], [Severe, Terminated],
            [Attention, Severe, Terminated], [Attention, Terminated, Severe], [Severe, Terminated, Attention],
            [Terminated, Severe, Attention], [Terminated, Attention, Severe], [Severe, Severe, Terminated],
            [Severe, Attention, Attention], [Severe, Attention, Terminated, Attention], [Attention, Severe, Attention, Terminated],
            [Attention, Attention with { Number = -2 }], [Attention, Attention with { Number = 1222 }], [Attention, Attention with { Number = 1205 }]
        ];
        foreach (var errors in rejected)
        {
            Assert.False(IsAttentionCancellation(provider, true, false, errors), "Unrelated error sequence was accepted as cancellation.");
        }
    }

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
        if (failure is not SqlException { Errors.Count: 1 or 3 } sql)
        {
            return false;
        }
        return IsAttentionCancellation(provider, requested, sql.InnerException is not null,
            sql.Errors.Cast<SqlError>().Select(error => new CancellationError(error.Number, error.State, error.Class, error.Procedure, error.LineNumber, error.Message)).ToArray());
    }

    // Pinned SqlClient 6.1.6: TdsParser adds DONE_ERROR and attention errors; the state
    // object appends warnings. The three-record shape was observed after actual cancellation.
    // Only these exact ordered shapes qualify; compare resources privately, never log them.
    private static bool IsAttentionCancellation(string provider, bool requested, bool hasInner, IReadOnlyList<CancellationError> errors) =>
        provider == "SqlServer" && requested && !hasInner &&
        (errors.Count == 1 && errors[0] == Attention ||
         errors.Count == 3 && errors[0] == Severe && errors[1] == Attention && errors[2] == Terminated);

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
