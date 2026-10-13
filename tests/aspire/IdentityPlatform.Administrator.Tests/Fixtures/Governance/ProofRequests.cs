using IdentityPlatform.Core.Features.Identity.Governance;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Builds alternate public intent through its legitimate factory before the original operation acquires database locks.</summary>
internal static class ProofRequests
{
    private const string OtherRole = "Auditor";
    private const string OtherPermission = "roles.read";
    private const long NextRevision = 1;

    /// <summary>Changes one reviewed intent dimension while retaining valid request shape; changed target includes its actual target stamp.</summary>
    internal static async Task<GovernanceRequest?> RebindAsync(GovernanceScenario scenario, GovernanceRequest original, string condition)
    {
        if (condition == ProofCases.Target)
        {
            var role = await GovernanceCommands.RoleAsync(scenario, OtherRole);
            var request = await scenario.RequestAsync(original.Action, roleId: role, permission: original.Permission);
            Assert.NotEqual(original.RoleId, request.RoleId);
            return request;
        }
        var altered = condition switch
        {
            ProofCases.Action => GovernanceRequest.Create(GovernanceAction.RemoveGrant, original.Versions,
                roleId: original.RoleId, permission: original.Permission),
            ProofCases.Payload => GovernanceRequest.Create(original.Action, original.Versions,
                roleId: original.RoleId, permission: OtherPermission),
            ProofCases.Version => GovernanceRequest.Create(original.Action,
                original.Versions with
                {
                    AuthorityRevision = original.Versions.AuthorityRevision + NextRevision
                },
                roleId: original.RoleId, permission: original.Permission),
            _ => null
        };
        if (condition is ProofCases.Action or ProofCases.Payload or ProofCases.Version)
        {
            Assert.NotNull(altered);
            Assert.True(altered.Action != original.Action || altered.Permission != original.Permission || altered.Versions != original.Versions);
        }
        return altered;
    }
}
