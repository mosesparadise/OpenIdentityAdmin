using System.Data;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Owns fresh observations for exactly one ALTER attempt and its later independent schema probe.</summary>
internal sealed class SqlPrivilegeCheck
{
    private SqlPrivilegeSnapshot? runtime;
    private SqlPrivilegeSnapshot? administrator;
    private Guid beforeAlter;
    private Guid afterAlter;
    private bool attemptedAlter;

    /// <summary>Returns this attempt's observations; absent observations remain explicit and cannot establish denial.</summary>
    internal SqlPrivilegeEvidence Evidence => new(runtime, administrator, attemptedAlter, beforeAlter, afterAlter);

    /// <summary>Explicitly holds the runtime connection open so metadata and ALTER use the same native session.</summary>
    internal async Task ReadRuntimeAsync(PlatformDbContext database, CancellationToken token)
    {
        await database.Database.OpenConnectionAsync(token);
        runtime = await SqlPrivilegeSnapshot.ReadAsync(database, token);
    }

    /// <summary>Marks an attempt only immediately before the unchanged native ALTER call.</summary>
    internal void StartingAlter(PlatformDbContext database)
    {
        beforeAlter = ConnectionId(database);
        attemptedAlter = true;
    }

    /// <summary>A closed or replaced connection cannot corroborate the original observation.</summary>
    internal void FinishedAlter(PlatformDbContext database) => afterAlter = ConnectionId(database);

    /// <summary>Reads independent administrator evidence only after the runtime attempt has completed or failed.</summary>
    internal async Task<ProbeOutcome> ProbeAsync(PlatformDbContext database, CancellationToken token)
    {
        administrator = await SqlPrivilegeSnapshot.ReadAsync(database, token);
        return administrator.ProbeColumns != 0 ? ProbeOutcome.Present :
            administrator.GovernanceRows != 1 ? ProbeOutcome.InvalidGovernance : ProbeOutcome.Absent;
    }

    /// <summary>Returns no usable identity for a connection that is no longer open.</summary>
    private static Guid ConnectionId(PlatformDbContext database)
    {
        var connection = (SqlConnection)database.Database.GetDbConnection();
        return connection.State == ConnectionState.Open ? connection.ClientConnectionId : Guid.Empty;
    }
}
