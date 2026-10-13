using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>independently establishes a persisted unaudited membership transition before a mutation marker is permitted.</summary>
internal static class GovernanceUnauditedCommit
{
    private const string Accepted = "Accepted";
    private const string AssignedEvent = "AdministratorBundleAssigned";
    private const string InvalidEvidence = "The independent persisted unaudited-commit evidence did not match the selected command.";

    /// <summary>Reads fresh stored state after actual accepted completion; assertion failures disclose no command or credential material.</summary>
    internal static async Task RequireAsync(GovernanceScenario scenario, GovernanceRequest request,
        int originalAcceptedAuditCount, CancellationToken token)
    {
        Assert.True(request.Action == GovernanceAction.AssignBundle && request.UserId != scenario.ActorId, InvalidEvidence);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.UserRoles.AsNoTracking().AnyAsync(row =>
            row.UserId == request.UserId && row.RoleId == request.RoleId, token), InvalidEvidence);
        var revision = await database.Set<AdministratorGovernance>().AsNoTracking()
            .Select(row => row.AuthorityRevision).SingleAsync(token);
        Assert.True(revision == checked(request.Versions.AuthorityRevision + 1), InvalidEvidence);
        Assert.True(!await database.Set<SecurityAuditIntent>().AsNoTracking().AnyAsync(row =>
            row.Event == AssignedEvent && row.Outcome == Accepted && row.ActorId == scenario.ActorId &&
            row.TargetId == request.UserId, token), InvalidEvidence);
        Assert.True(await database.Set<SecurityAuditIntent>().AsNoTracking().CountAsync(row =>
            row.Outcome == Accepted, token) == originalAcceptedAuditCount, InvalidEvidence);
    }
}
