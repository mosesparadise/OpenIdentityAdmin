using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Observes real successful replay pruning without retaining SQL, credential values or provider diagnostics.</summary>
internal sealed class ProofReplayEvidence : DbCommandInterceptor
{
    private const string PostgreSqlReplayTable = "\"AdministratorReplay\"";
    private const string SqlServerReplayTable = "[AdministratorReplay]";
    private const string Delete = "DELETE";
    private bool armed;
    private int affected;
    private int positiveCommands;

    /// <summary>Arms the observation after fixture setup and discards any prior enrollment cleanup observations.</summary>
    internal void Begin()
    {
        affected = 0;
        positiveCommands = 0;
        armed = true;
    }

    /// <summary>Stops observing before after-state reads and any fixture cleanup begins.</summary>
    internal void Stop() => armed = false;

    /// <summary>Requires exactly one row to have actually been deleted before the denial savepoint restores it.</summary>
    internal void RequireSinglePrunedRow()
    {
        Assert.Equal(1, positiveCommands);
        Assert.Equal(1, affected);
    }

    /// <summary>Counts only completed asynchronous DELETE commands against the exact native replay table and never replaces results.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (armed && result > 0 && command.CommandText.Contains(Delete, StringComparison.Ordinal) &&
            (command.CommandText.Contains(PostgreSqlReplayTable, StringComparison.Ordinal) ||
             command.CommandText.Contains(SqlServerReplayTable, StringComparison.Ordinal)))
        {
            affected = checked(affected + result);
            positiveCommands++;
        }
        return ValueTask.FromResult(result);
    }
}
