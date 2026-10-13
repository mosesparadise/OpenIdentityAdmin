using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Checks the native writer's required semantic version before independent typed commit defenses run.</summary>
internal static class GovernanceVersionWriteEvidence
{
    private const string PermissionType = "identityplatform.permission";
    private const string Bypass = "governance-version-bypass";
    private const string MissingEffect = "The real protected grant write did not complete, so no version mutation credit is available.";

    /// <summary>Obtains real private proof, performs the actual protected write and checks persisted/tracked versions before rolling everything back.</summary>
    internal static async Task RequireAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        Assert.Equal(GovernanceAction.AddGrant, request.Action);
        var before = await scenario.SnapshotAsync();
        await using (var owner = await ProofOwner.OpenAsync(scenario, request))
        {
            var operation = owner.Operation;
            var database = operation.Get<PlatformDbContext>();
            var transaction = operation.Transaction;
            var clock = operation.Get<CeremonyClock>();
            await operation.Get<CeremonyBudget>().CompleteGovernanceAsync(transaction, operation.State.Actor, owner.Proof);
            await operation.Get<GovernanceMutation>().ApplyAsync(transaction, operation.State, owner.Proof, clock.Now);
            Assert.True(await database.RoleClaims.AsNoTracking().AnyAsync(row => row.RoleId == request.RoleId &&
                row.ClaimType == PermissionType && row.ClaimValue == request.Permission, transaction.Token), MissingEffect);
            var persisted = await database.Set<AdministratorGovernance>().AsNoTracking().Select(row => row.AuthorityRevision).SingleAsync(transaction.Token);
            var expected = checked(request.Versions.AuthorityRevision + 1);
            Assert.True(persisted == expected && operation.State.Authority.AuthorityRevision == expected, Bypass);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
