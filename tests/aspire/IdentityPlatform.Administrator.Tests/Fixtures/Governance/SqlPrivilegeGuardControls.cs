using IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Seeds synthetic proof values independently of the native metadata reader and production permissions.</summary>
internal static class SqlPrivilegeGuardControls
{
    private static readonly Guid RuntimeConnection = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherConnection = new("22222222-2222-2222-2222-222222222222");
    private const int ExistingObject = 42;
    private const int OtherObject = 43;
    private const string Database = "test-database";
    private const string Server = "test-server";
    private const string OtherDatabase = "other-database";
    private const string OtherServer = "other-server";

    /// <summary>Constructs a fully corroborated denial with a separate administrator observation.</summary>
    internal static SqlPrivilegeEvidence Complete()
    {
        var runtime = new SqlPrivilegeSnapshot(Database, Server, ExistingObject, RuntimeConnection, true, true, 0, 0, 1);
        var administrator = runtime with
        {
            ConnectionId = OtherConnection,
            RuntimePrincipal = false,
            AlterPermission = 1
        };
        return new(runtime, administrator, true, RuntimeConnection, RuntimeConnection);
    }

    /// <summary>Changes exactly the selected evidence field; missing records never fall back to native-denial acceptance.</summary>
    internal static SqlPrivilegeEvidence? Without(SqlPrivilegeGuardCase condition)
    {
        var proof = Complete();
        return condition switch
        {
            SqlPrivilegeGuardCase.MissingEvidence => null,
            SqlPrivilegeGuardCase.MissingRuntime => proof with { Runtime = null },
            SqlPrivilegeGuardCase.MissingAdministrator => proof with { Administrator = null },
            SqlPrivilegeGuardCase.ChangedBeforeConnection => proof with { BeforeAlter = OtherConnection },
            SqlPrivilegeGuardCase.ChangedAfterConnection => proof with { AfterAlter = OtherConnection },
            SqlPrivilegeGuardCase.ClosedBeforeConnection => proof with { BeforeAlter = Guid.Empty },
            SqlPrivilegeGuardCase.ClosedAfterConnection => proof with { AfterAlter = Guid.Empty },
            SqlPrivilegeGuardCase.NoAlterAttempt => proof with { AttemptedAlter = false },
            SqlPrivilegeGuardCase.EmptyRuntimeConnection => proof with
            {
                Runtime = proof.Runtime! with
                {
                    ConnectionId = Guid.Empty
                },
                BeforeAlter = Guid.Empty,
                AfterAlter = Guid.Empty
            },
            _ => proof with { Runtime = ChangeRuntime(proof.Runtime!, condition), Administrator = ChangeAdministrator(proof.Administrator!, condition) }
        };
    }

    /// <summary>Invalidates runtime identity, target, effective permission or pre-attempt state independently.</summary>
    private static SqlPrivilegeSnapshot ChangeRuntime(SqlPrivilegeSnapshot value, SqlPrivilegeGuardCase condition) => condition switch
    {
        SqlPrivilegeGuardCase.RuntimeModel => value with { ModelMatches = false },
        SqlPrivilegeGuardCase.EmptyDatabase => value with { Database = "" },
        SqlPrivilegeGuardCase.EmptyServer => value with { Server = "" },
        SqlPrivilegeGuardCase.MissingRuntimeObject => value with { ObjectId = null },
        SqlPrivilegeGuardCase.NonpositiveObject => value with { ObjectId = 0 },
        SqlPrivilegeGuardCase.WrongPrincipal => value with { RuntimePrincipal = false },
        SqlPrivilegeGuardCase.UnknownRuntimePermission => value with { AlterPermission = null },
        SqlPrivilegeGuardCase.GrantedRuntimePermission => value with { AlterPermission = 1 },
        SqlPrivilegeGuardCase.RuntimeProbePresent => value with { ProbeColumns = 1 },
        SqlPrivilegeGuardCase.MissingRuntimeSingleton => value with { GovernanceRows = 0 },
        _ => value
    };

    /// <summary>Invalidates the independent post-attempt observation without changing the runtime observation.</summary>
    private static SqlPrivilegeSnapshot ChangeAdministrator(SqlPrivilegeSnapshot value, SqlPrivilegeGuardCase condition) => condition switch
    {
        SqlPrivilegeGuardCase.AdministratorModel => value with { ModelMatches = false },
        SqlPrivilegeGuardCase.EmptyDatabase => value with { Database = "" },
        SqlPrivilegeGuardCase.EmptyServer => value with { Server = "" },
        SqlPrivilegeGuardCase.NonpositiveObject => value with { ObjectId = 0 },
        SqlPrivilegeGuardCase.DifferentDatabase => value with { Database = OtherDatabase },
        SqlPrivilegeGuardCase.DifferentServer => value with { Server = OtherServer },
        SqlPrivilegeGuardCase.MissingAdministratorObject => value with { ObjectId = null },
        SqlPrivilegeGuardCase.DifferentObject => value with { ObjectId = OtherObject },
        SqlPrivilegeGuardCase.UnknownAdministratorPermission => value with { AlterPermission = null },
        SqlPrivilegeGuardCase.DeniedAdministratorPermission => value with { AlterPermission = 0 },
        SqlPrivilegeGuardCase.AdministratorProbePresent => value with { ProbeColumns = 1 },
        SqlPrivilegeGuardCase.MissingAdministratorSingleton => value with { GovernanceRows = 0 },
        _ => value
    };
}
