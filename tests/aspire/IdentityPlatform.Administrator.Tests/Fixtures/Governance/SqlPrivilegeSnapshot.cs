using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Private native context values bind the runtime attempt to the independent administrator probe; never log this record.</summary>
internal sealed record SqlPrivilegeSnapshot(string Database, string Server, int? ObjectId, Guid ConnectionId,
    bool ModelMatches, bool RuntimePrincipal, int? AlterPermission, int ProbeColumns, int GovernanceRows)
{
    private const int DeadlineSeconds = 5;
    private const string TargetSchema = "platform";
    private const string TargetTable = "AdministratorGovernance";
    private const int ObjectOrdinal = 2;
    private const int PrincipalOrdinal = 3;
    private const int PermissionOrdinal = 4;
    private const int ProbeOrdinal = 5;
    private const int GovernanceOrdinal = 6;
    private const string MissingRow = "Missing SQL privilege observation row.";
    private const string ExtraRow = "Extra SQL privilege observation row.";
    private const string Query = """
        SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')),
          OBJECT_ID(N'platform.AdministratorGovernance', N'U'),
          CASE WHEN USER_NAME() = N'platform_runtime' AND ORIGINAL_LOGIN() = N'platform_runtime' THEN 1 ELSE 0 END,
          HAS_PERMS_BY_NAME(N'platform.AdministratorGovernance', N'OBJECT', N'ALTER'),
          (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'platform.AdministratorGovernance', N'U')
            AND name = N'GovernancePrivilegeProbe'),
          (SELECT COUNT(*) FROM [platform].[AdministratorGovernance]);
        """;

    /// <summary>Reads the already open connection within the caller's existing deadline; permission NULL remains unknown.</summary>
    internal static async Task<SqlPrivilegeSnapshot> ReadAsync(PlatformDbContext database, CancellationToken token)
    {
        var entity = database.Model.FindEntityType(typeof(AdministratorGovernance));
        var modelMatches = entity?.GetSchema() == TargetSchema && entity.GetTableName() == TargetTable;
        var connection = (SqlConnection)database.Database.GetDbConnection();
        var connectionId = connection.ClientConnectionId;
        await using var command = connection.CreateCommand();
        command.CommandTimeout = DeadlineSeconds;
        command.CommandText = Query;
        await using var reader = await command.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token), MissingRow);
        var result = new SqlPrivilegeSnapshot(reader.GetString(0), reader.GetString(1),
            reader.IsDBNull(ObjectOrdinal) ? null : reader.GetInt32(ObjectOrdinal), connectionId, modelMatches, reader.GetInt32(PrincipalOrdinal) == 1,
            reader.IsDBNull(PermissionOrdinal) ? null : reader.GetInt32(PermissionOrdinal), reader.GetInt32(ProbeOrdinal), reader.GetInt32(GovernanceOrdinal));
        Assert.False(await reader.ReadAsync(token), ExtraRow);
        return result;
    }
}
