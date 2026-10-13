using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Establishes real durable command effects independently before any security mutation marker is emitted.</summary>
internal static class GovernanceDurableMutationEvidence
{
    private const string Accepted = "Accepted";
    private const string Assigned = "AdministratorBundleAssigned";
    private const string Removed = "AdministratorBundleRemoved";
    private const string Platform = "PlatformAdministrator";
    private const string NormalizedPlatform = "PLATFORMADMINISTRATOR";
    private const string Active = "Active";
    private const string FullMethods = "pwd otp";
    private const string InvalidEvidence = "Durable native command evidence does not match the selected security defect.";
    internal sealed record Before(long Authority, long Authentication, long Factor, int Replay, Guid[] Audits);

    /// <summary>Captures independent native counters and audit identities before the real command is submitted.</summary>
    internal static async Task<Before> CaptureAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var authority = await database.Set<AdministratorGovernance>().AsNoTracking().Select(row => row.AuthorityRevision).SingleAsync(token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().Where(row => row.UserId == scenario.ActorId)
            .Select(row => row.Generation).SingleAsync(token);
        var factor = await database.Set<AdministratorEnrollment>().AsNoTracking().Where(row => row.UserId == scenario.ActorId)
            .Select(row => row.FactorGeneration).SingleAsync(token);
        var replay = await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId, token);
        var audits = await database.Set<SecurityAuditIntent>().Select(row => row.Id).ToArrayAsync(token);
        return new Before(authority, authentication, factor, replay, audits);
    }

    /// <summary>Requires exact membership persistence, one semantic advance, a new replay claim and one independently attributed accepted audit.</summary>
    internal static async Task RequireMembershipCommitAsync(GovernanceScenario scenario, GovernanceRequest request, Before before)
    {
        Assert.True(request.Action is GovernanceAction.AssignBundle or GovernanceAction.RemoveBundle, InvalidEvidence);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var assigned = request.Action == GovernanceAction.AssignBundle;
        Assert.True(await database.UserRoles.AsNoTracking().AnyAsync(row => row.UserId == request.UserId && row.RoleId == request.RoleId, token)
            == assigned, InvalidEvidence);
        Assert.True(await database.Set<AdministratorGovernance>().AsNoTracking().Select(row => row.AuthorityRevision).SingleAsync(token)
            == checked(before.Authority + 1), InvalidEvidence);
        Assert.True(await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId, token)
            == checked(before.Replay + 1), InvalidEvidence);
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => !before.Audits.Contains(row.Id)).ToArrayAsync(token));
        Assert.True(audit.ActorId == scenario.ActorId && audit.TargetId == request.UserId && audit.Outcome == Accepted &&
            audit.Event == (assigned ? Assigned : Removed) && audit.CorrelationId != Guid.Empty, InvalidEvidence);
    }

    /// <summary>Proves zero possible platform survivors by native membership absence while the sole configured account retains normal credentials.</summary>
    internal static async Task RequireZeroPlatformSurvivorsAsync(GovernanceScenario scenario, GovernanceRequest request, Before before)
    {
        Assert.True(request.Action == GovernanceAction.RemoveBundle && request.UserId == scenario.ActorId, InvalidEvidence);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var role = await database.Roles.AsNoTracking().SingleAsync(row => row.Name == Platform, token);
        Assert.True(role.Id == request.RoleId && role.NormalizedName == NormalizedPlatform, InvalidEvidence);
        Assert.False(await database.UserRoles.AnyAsync(row => row.RoleId == role.Id, token), InvalidEvidence);
        var user = Assert.Single(await database.Users.AsNoTracking().ToArrayAsync(token));
        Assert.True(user.Id == scenario.ActorId && user.IsEnabled && user.TwoFactorEnabled && user.LockoutEnabled &&
            !string.IsNullOrEmpty(user.SecurityStamp) && !string.IsNullOrEmpty(user.PasswordHash) && user.AccessFailedCount == 0 &&
            !(user.LockoutEnd > scenario.Journey.Clock.Now), InvalidEvidence);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(row => row.UserId == user.Id, token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(row => row.UserId == user.Id, token);
        Assert.True(enrollment.State == Active && !enrollment.Suspended && enrollment.FactorGeneration == before.Factor && before.Factor > 0 &&
            authentication.Generation == before.Authentication && authentication.Failures == 0 && authentication.LockoutUntil is null, InvalidEvidence);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        Assert.True(await users.HasPasswordAsync(user) && !string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)), InvalidEvidence);
        var session = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(row => row.PublicId == scenario.Session.SessionId, token);
        Assert.True(!session.Revoked && session.UserId == user.Id && session.SecurityStamp == user.SecurityStamp &&
            session.Generation == before.Authentication && session.FactorGeneration == before.Factor && session.AuthenticationMethods == FullMethods, InvalidEvidence);
    }
}
