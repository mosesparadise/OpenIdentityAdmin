using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Classifies only the selected native audit failure without retaining SQL, parameters or exception text.</summary>
internal sealed class GovernanceAuditFailure : DbCommandInterceptor
{
    private const string Insert = "INSERT";
    private const string Audit = "SecurityAuditIntent";
    private const string PostgreSqlPermission = "42501";
    private const int SqlPermission = 229;
    private bool armed;
    private bool sqlAllowed;
    private Func<bool>? callerCancelled;
    internal bool PermissionDenied
    {
        get; private set;
    }
    internal bool CallerCancelled
    {
        get; private set;
    }

    /// <summary>Arms only after setup; cancellation credit requires the explicitly owned caller to have requested it.</summary>
    internal void Arm(bool sql, Func<bool>? cancelled = null)
    {
        armed = true;
        sqlAllowed = sql;
        callerCancelled = cancelled;
    }

    /// <summary>Checks the actual native error before production sanitizes it into unavailable completion.</summary>
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (Matches(command))
        {
            PermissionDenied |= eventData.Exception is Npgsql.PostgresException { SqlState: PostgreSqlPermission } ||
                eventData.Exception is Microsoft.Data.SqlClient.SqlException { Number: SqlPermission };
            CallerCancelled |= CeremonyCancellation.IsCallerCancellation(eventData.Exception, sqlAllowed, callerCancelled?.Invoke() == true);
        }
        return Task.CompletedTask;
    }

    /// <summary>Recognizes EF's cancelled-command event only for the actual blocked audit and requested caller token.</summary>
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        CallerCancelled |= Matches(command) && callerCancelled?.Invoke() == true && cancellationToken.IsCancellationRequested;
        return Task.CompletedTask;
    }

    /// <summary>Matches only the armed provider statement; unrelated startup or account failures cannot satisfy this oracle.</summary>
    private bool Matches(DbCommand command) => armed && command.CommandText.Contains(Insert, StringComparison.Ordinal) &&
        command.CommandText.Contains(Audit, StringComparison.Ordinal);
}
