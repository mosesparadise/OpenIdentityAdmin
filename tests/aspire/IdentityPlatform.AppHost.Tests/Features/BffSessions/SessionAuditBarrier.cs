using System.Data.Common;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Pauses the native audit command after ticket insertion to exercise rejected overlapping participant operations deterministically.</summary>
internal sealed class SessionAuditBarrier : DbCommandInterceptor
{
    private const string Insert = "INSERT";
    private const string AuditTable = "AdminSessionAudit";
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Entered => entered.Task;

    /// <summary>Holds only audit insertion without using a second EF operation on the busy context.</summary>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(Insert, StringComparison.Ordinal) && command.CommandText.Contains(AuditTable, StringComparison.Ordinal))
        {
            entered.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Always releases and joins the native write while preserving the first assertion or execution failure.</summary>
    internal async Task CompleteAsync(Task pending, ExceptionDispatchInfo? failure)
    {
        released.TrySetResult();
        try
        {
            await pending;
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        failure?.Throw();
    }
}
