using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Moves the proof clock only after the provider really deletes an assertion tombstone.</summary>
internal sealed class NativeAssertionCleanupClock : DbCommandInterceptor
{
    private const string DeleteVerb = "DELETE";
    private const string AssertionTable = "AdministratorClientAssertionUse";
    internal Action? AfterDelete
    {
        get; set;
    }

    /// <summary>Consumes one exact post-delete observation, leaving ordinary command execution and cancellation intact.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (result > 0 && command.CommandText.TrimStart().StartsWith(DeleteVerb, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(AssertionTable, StringComparison.Ordinal))
        {
            var action = AfterDelete;
            AfterDelete = null;
            action?.Invoke();
        }
        return ValueTask.FromResult(result);
    }
}
