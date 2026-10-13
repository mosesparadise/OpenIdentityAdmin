namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Combines observations from one SQL attempt without reclassifying its raw provider result.</summary>
internal sealed record SqlPrivilegeEvidence(SqlPrivilegeSnapshot? Runtime, SqlPrivilegeSnapshot? Administrator,
    bool AttemptedAlter, Guid BeforeAlter, Guid AfterAlter)
{
    private const int PermissionDenied = 229;
    private const int ObjectPermissionDenied = 4902;
    private const int ObjectUnavailable = 1088;

    /// <summary>Fails closed unless the exact visible object, principal, denied permission and stable connection agree.</summary>
    internal bool Supports(AlterObservation alter, ProbeOutcome probe)
    {
        var nativeDenial = alter is { Outcome: AlterOutcome.Denied, ProviderCode: PermissionDenied or ObjectPermissionDenied };
        var ambiguousDenial = alter is { Outcome: AlterOutcome.ProviderError, ProviderCode: ObjectUnavailable };
        return (nativeDenial || ambiguousDenial) && probe == ProbeOutcome.Absent && AttemptedAlter &&
            Runtime is { ModelMatches: true, RuntimePrincipal: true, AlterPermission: 0, ProbeColumns: 0, GovernanceRows: 1 } &&
            Administrator is { ModelMatches: true, AlterPermission: 1, ProbeColumns: 0, GovernanceRows: 1 } &&
            !string.IsNullOrWhiteSpace(Runtime.Server) && !string.IsNullOrWhiteSpace(Runtime.Database) &&
            string.Equals(Runtime.Server, Administrator.Server, StringComparison.Ordinal) &&
            string.Equals(Runtime.Database, Administrator.Database, StringComparison.Ordinal) &&
            Runtime.ObjectId is > 0 && Runtime.ObjectId == Administrator.ObjectId &&
            Runtime.ConnectionId != Guid.Empty && Runtime.ConnectionId == BeforeAlter && BeforeAlter == AfterAlter;
    }
}
