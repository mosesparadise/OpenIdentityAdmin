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
    private const string DenialMessage = "Expected native ALTER permission denial; raw provider diagnostics are withheld.";

    /// <summary>Requires accepted real runtime DML before attempting a schema write through that same least-privilege principal.</summary>
    internal static async Task RequireAsync(BootstrapFixture fixture, string provider)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RemoveGrant);
        Assert.Equal(GovernanceOutcome.Accepted, await scenario.ExecuteAsync(request));
        await RequireCommittedAsync(scenario, request);
        var before = await scenario.SnapshotAsync();
        await RequireAlterDeniedAsync(scenario, provider);
        Assert.Equal(before, await scenario.SnapshotAsync());
        await RequireProbeAbsentAsync(scenario);
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
    private static async Task RequireAlterDeniedAsync(GovernanceScenario scenario, string provider)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        database.Database.SetCommandTimeout(DeadlineSeconds);
        var denied = false;
        try
        {
            await database.Database.ExecuteSqlRawAsync(provider == PostgreSql ? PostgreSqlAlter : SqlServerAlter, deadline.Token);
        }
        catch (DbException exception)
        {
            denied = provider == PostgreSql
                ? exception is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.InsufficientPrivilege }
                : exception is Microsoft.Data.SqlClient.SqlException { Number: SqlPermissionDenied or SqlObjectPermissionDenied };
        }
        catch (OperationCanceledException) { /* A timeout does not demonstrate privilege isolation. */ }
        Assert.True(denied, DenialMessage);
    }

    /// <summary>Independently confirms object existence and unchanged schema with migration-owned metadata visibility.</summary>
    private static async Task RequireProbeAbsentAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.Journey.Lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = ProbeCountQuery;
        command.CommandTimeout = DeadlineSeconds;
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, await database.Set<AdministratorGovernance>().CountAsync(TestContext.Current.CancellationToken));
    }
}
