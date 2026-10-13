using System.Data.Common;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Distinguishes real native permission denial from timeout, syntax failure or an unavailable database.</summary>
internal static class GovernancePrivileges
{
    private const string GrantRemoved = "AdministratorGrantRemoved";
    private const string Accepted = "Accepted";
    private const string ProbeCountQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'AdministratorGovernance' AND COLUMN_NAME = 'GovernancePrivilegeProbe'";
    private const int DeadlineSeconds = 5;
    private const string PostgreSql = "PostgreSql";
    private const string PostgreSqlAlter = "ALTER TABLE platform.\"AdministratorGovernance\" ADD COLUMN \"GovernancePrivilegeProbe\" integer NULL";
    private const string SqlServerAlter = "ALTER TABLE [platform].[AdministratorGovernance] ADD [GovernancePrivilegeProbe] int NULL";
    private const int SqlPermissionDenied = 229;
    private const int SqlObjectPermissionDenied = 4902;

    /// <summary>Requires accepted real runtime DML before attempting a schema write through that same least-privilege principal.</summary>
    internal static async Task RequireAsync(BootstrapFixture fixture, string provider)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RemoveGrant);
        Assert.Equal(GovernanceOutcome.Accepted, await scenario.ExecuteAsync(request));
        await RequireCommittedAsync(scenario, request);
        var before = await scenario.SnapshotAsync();
        var sql = provider == PostgreSql ? null : new SqlPrivilegeCheck();
        var observed = await PrivilegeDiagnostics.ObserveAsync(() => ObserveAlterAsync(scenario, provider, sql), () => ObserveProbeAsync(scenario, sql));
        observed.Write();
        if (sql is null)
        {
            observed.RequireDenial();
        }
        else
        {
            observed.RequireSqlDenial(sql.Evidence);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Checks stored native effect, semantic advance and exact accepted audit rather than trusting the returned outcome alone.</summary>
    private static async Task RequireCommittedAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.False(await database.RoleClaims.AnyAsync(row => row.RoleId == request.RoleId && row.ClaimValue == request.Permission, token));
        Assert.Equal(request.Versions.AuthorityRevision + 1, await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == GrantRemoved &&
            row.Outcome == Accepted && row.ActorId == scenario.ActorId && row.TargetId == request.RoleId, token));
    }

    /// <summary>Accepts only provider-specific privilege failure within a linked five-second ceiling; cancellation is not a passing oracle.</summary>
    private static async Task<AlterObservation> ObserveAlterAsync(GovernanceScenario scenario, string provider, SqlPrivilegeCheck? sql)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        database.Database.SetCommandTimeout(DeadlineSeconds);
        if (sql is not null)
        {
            await sql.ReadRuntimeAsync(database, deadline.Token);
        }
        try
        {
            sql?.StartingAlter(database);
            await database.Database.ExecuteSqlRawAsync(provider == PostgreSql ? PostgreSqlAlter : SqlServerAlter, deadline.Token);
            return new(AlterOutcome.Succeeded, null);
        }
        catch (DbException exception)
        {
            var denied = provider == PostgreSql
                ? exception is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.InsufficientPrivilege }
                : exception is Microsoft.Data.SqlClient.SqlException { Number: SqlPermissionDenied or SqlObjectPermissionDenied };
            return new(denied ? AlterOutcome.Denied : AlterOutcome.ProviderError,
                exception is Microsoft.Data.SqlClient.SqlException sqlError ? sqlError.Number : null);
        }
        catch (OperationCanceledException) { return new(AlterOutcome.Cancelled, null); }
        finally { sql?.FinishedAlter(database); }
    }

    /// <summary>Independently confirms object existence and unchanged schema with migration-owned metadata visibility.</summary>
    private static async Task<ProbeOutcome> ObserveProbeAsync(GovernanceScenario scenario, SqlPrivilegeCheck? sql)
    {
        var lifetime = TestContext.Current.CancellationToken;
        if (lifetime.IsCancellationRequested)
        {
            return ProbeOutcome.NotObserved;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        await using var scope = scenario.Journey.Lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Database.SetCommandTimeout(DeadlineSeconds);
        await database.Database.OpenConnectionAsync(deadline.Token);
        if (sql is not null)
        {
            return await sql.ProbeAsync(database, deadline.Token);
        }
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = ProbeCountQuery;
        command.CommandTimeout = DeadlineSeconds;
        var columns = Convert.ToInt32(await command.ExecuteScalarAsync(deadline.Token), System.Globalization.CultureInfo.InvariantCulture);
        var governance = await database.Set<AdministratorGovernance>().CountAsync(deadline.Token);
        return columns != 0 ? ProbeOutcome.Present : governance != 1 ? ProbeOutcome.InvalidGovernance : ProbeOutcome.Absent;
    }
}
