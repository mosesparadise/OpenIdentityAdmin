using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Injects one acknowledgement failure only after a real native bulk update affected rows.</summary>
internal sealed class NativeRefreshRevocationFault : DbCommandInterceptor
{
    private const string UpdateVerb = "UPDATE";
    private const string TokenTable = "OpenIddictTokens";
    private const string Failure = "Synthetic refresh revocation acknowledgement loss.";
    internal bool Armed
    {
        get; set;
    }
    internal bool Reached
    {
        get; private set;
    }

    /// <summary>Records the exact post-provider boundary before throwing; ordinary and restored calls retain native behavior.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (Armed && result > 0 && command.CommandText.TrimStart().StartsWith(UpdateVerb, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(TokenTable, StringComparison.Ordinal))
        {
            Armed = false;
            Reached = true;
            throw new InvalidOperationException(Failure);
        }
        return ValueTask.FromResult(result);
    }
}
