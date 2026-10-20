using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Fails observation after the provider actually deletes native tokens, requiring rollback of real cleanup rather than an unexecuted command.</summary>
internal sealed class NativeTokenCleanupFault : DbCommandInterceptor
{
    private const string DeleteVerb = "DELETE";
    private const string TokenTable = "OpenIddictTokens";
    internal Action? AfterDelete
    {
        get; set;
    }

    /// <summary>Consumes the fault once only after a matching native token delete affected at least one row.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (result > 0 && command.CommandText.TrimStart().StartsWith(DeleteVerb, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(TokenTable, StringComparison.Ordinal))
        {
            var action = AfterDelete;
            AfterDelete = null;
            action?.Invoke();
        }
        return ValueTask.FromResult(result);
    }
}
