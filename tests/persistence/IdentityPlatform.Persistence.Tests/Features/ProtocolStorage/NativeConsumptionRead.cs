using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Mutates a live continuation only after the final native authorization read really executes on the selected provider.</summary>
internal sealed class NativeConsumptionRead : DbCommandInterceptor
{
    private const string SelectVerb = "SELECT";
    private const string AuthorizationTable = "OpenIddictAuthorizations";
    internal Action? AfterAuthorization
    {
        get; set;
    }

    /// <summary>Consumes one armed asynchronous read observation while preserving the real reader and cancellation.</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith(SelectVerb, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(AuthorizationTable, StringComparison.Ordinal))
        {
            var action = AfterAuthorization;
            AfterAuthorization = null;
            action?.Invoke();
        }
        return ValueTask.FromResult(result);
    }
}
