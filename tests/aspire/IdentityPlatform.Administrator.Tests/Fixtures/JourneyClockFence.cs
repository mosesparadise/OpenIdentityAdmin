using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Moves only the policy clock after an observed real provider read, never replacing native factor verification.</summary>
internal sealed class JourneyClockFence : DbCommandInterceptor
{
    private Action? action;
    private string marker = string.Empty;
    private string? operation;
    internal bool Triggered
    {
        get; private set;
    }

    /// <summary>Arms one precise provider boundary and records whether the requested boundary was actually observed.</summary>
    internal void Arm(string commandMarker, Action advance, string? commandOperation = null)
    {
        marker = commandMarker;
        operation = commandOperation;
        action = advance;
        Triggered = false;
    }

    /// <summary>Advances once after the native read completes so final decision resampling is independently exercised.</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(marker, StringComparison.Ordinal) &&
            (operation is null || command.CommandText.Contains(operation, StringComparison.Ordinal)) &&
            Interlocked.Exchange(ref action, null) is { } advance)
        {
            Triggered = true;
            advance();
        }
        return ValueTask.FromResult(result);
    }
}
