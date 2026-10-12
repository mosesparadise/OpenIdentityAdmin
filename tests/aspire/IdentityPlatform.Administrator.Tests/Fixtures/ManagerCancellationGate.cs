using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

public sealed class ManagerCancellationGate : DbCommandInterceptor
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal) && command.CommandText.Contains("AspNetRoleClaims", StringComparison.Ordinal))
        {
            Assert.True(cancellationToken.CanBeCanceled && !cancellationToken.IsCancellationRequested, "Native manager INSERT did not receive the live operation token.");
            Entered.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        return result;
    }
}
