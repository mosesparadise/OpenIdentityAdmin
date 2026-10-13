using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Checks sole-administrator governance independently of restricted recovery and genuine replacement-factor authority.</summary>
internal static class GovernanceRecoveryState
{
    private const string Platform = "PlatformAdministrator";
    private const string Specialist = "IdentityAdministrator";
    private const string Permission = "users.read";
    private const string RecoveryRequired = "RecoveryRequired";
    private const string Active = "Active";

    /// <summary>Creates a pending nominee, establishes a nonzero committed authority revision, and proves neither nominee nor unused recovery material permits last removal.</summary>
    internal static async Task<long> PrepareAsync(GovernanceHttpBridge bridge)
    {
        _ = await GovernanceAccounts.AddAsync(bridge.CreateScope, GovernanceAccounts.Password(bridge.Original.Password), usable: false);
        var role = await RoleAsync(bridge, Specialist);
        var removeGrant = await bridge.RequestAsync(GovernanceAction.RemoveGrant, role: role, permission: Permission);
        Assert.Equal(GovernanceOutcome.Accepted, await bridge.ExecuteAsync(bridge.Original, removeGrant));
        var observation = await bridge.ObserveAsync();
        Assert.Equal(bridge.Original.UserId, Assert.Single(observation.UsableAdministrators));
        Assert.True(observation.AuthorityRevision > 0);
        var request = await bridge.RequestAsync(GovernanceAction.RemoveBundle, bridge.Original.UserId, await RoleAsync(bridge, Platform));
        var before = await bridge.SnapshotAsync();
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => bridge.ExecuteAsync(bridge.Original, request));
        Assert.Equal(before, await bridge.SnapshotAsync());
        return observation.AuthorityRevision;
    }

    /// <summary>Reads actual native recovery verifier rows without exposing them as assertion text.</summary>
    internal static async Task<AdministratorRecoveryCode[]> CodesAsync(GovernanceHttpBridge bridge)
    {
        await using var scope = bridge.CreateScope();
        return await SecuritySnapshot.RecoveryAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    /// <summary>Requires the spent-code retry to retain every verifier and redemption timestamp without printing recovery material.</summary>
    internal static async Task RequireCodesUnchangedAsync(GovernanceHttpBridge bridge, AdministratorRecoveryCode[] before)
    {
        var after = await CodesAsync(bridge);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(before))),
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(after))));
    }

    /// <summary>Requires one real recovery claim and zero full governance population while replacement is restricted.</summary>
    internal static async Task<long> RequireRestrictedAsync(GovernanceHttpBridge bridge, AdministratorRecoveryCode[] before, string submitted, long revision)
    {
        SecuritySnapshot.RequireSingleClaim(before, await CodesAsync(bridge), submitted);
        var observed = await bridge.ObserveAsync();
        Assert.Empty(observed.Permissions);
        Assert.Empty(observed.UsableAdministrators);
        Assert.Equal(revision, observed.AuthorityRevision);
        await using var scope = bridge.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(row => row.UserId == bridge.Original.UserId, token);
        Assert.Equal(RecoveryRequired, enrollment.State);
        Assert.False(await database.Set<AdministratorSession>().AnyAsync(row => !row.Revoked, token));
        var replacement = await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(row => row.UserId == bridge.Original.UserId && row.StagedSeed != null, token);
        Assert.Null(replacement.SessionPublicId);
        Assert.True(replacement.DestinationGeneration > enrollment.FactorGeneration);
        await RequireRestrictedCommandAsync(bridge, replacement.PublicId);
        return replacement.DestinationGeneration;
    }

    /// <summary>Presents the real restricted interaction identifier as untrusted command-session input and requires exact nonmutating governance denial.</summary>
    private static async Task RequireRestrictedCommandAsync(GovernanceHttpBridge bridge, string interaction)
    {
        var candidate = new GovernanceHttpActor(new JourneySessionCandidate(bridge.Original.UserId, interaction),
            bridge.Original.Password, bridge.Original.Seed);
        var request = await bridge.RequestAsync(GovernanceAction.AddGrant, role: await RoleAsync(bridge, Specialist), permission: Permission);
        var before = await bridge.SnapshotAsync();
        Assert.Equal(GovernanceOutcome.Denied, await bridge.ExecuteAsync(candidate, request));
        Assert.Equal(before, await bridge.SnapshotAsync());
    }

    /// <summary>Requires active enrollment at the exact staged destination and a single genuinely new stored session after native HTTP completion.</summary>
    internal static async Task<GovernanceHttpActor> RequireCompletedAsync(GovernanceHttpBridge bridge, string newSeed, long destination, long revision)
    {
        var observed = await bridge.ObserveAsync();
        Assert.Equal(bridge.Original.UserId, Assert.Single(observed.UsableAdministrators));
        Assert.NotEmpty(observed.Permissions);
        Assert.Equal(revision, observed.AuthorityRevision);
        await using var scope = bridge.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(row => row.UserId == bridge.Original.UserId, token);
        Assert.Equal(Active, enrollment.State);
        Assert.Equal(destination, enrollment.FactorGeneration);
        var session = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(row => !row.Revoked, token);
        Assert.Equal(bridge.Original.UserId, session.UserId);
        Assert.True(session.PublicId != bridge.Original.Session.SessionId);
        Assert.Equal(destination, session.FactorGeneration);
        return new GovernanceHttpActor(new JourneySessionCandidate(session.UserId, session.PublicId), bridge.Original.Password, newSeed);
    }

    /// <summary>Proves recovery did not repair the original command session; only the newly verified factor/session can restore the removed specialist grant.</summary>
    internal static async Task RequireGovernanceAsync(GovernanceHttpBridge bridge, GovernanceHttpActor replacement, long revision)
    {
        var request = await bridge.RequestAsync(GovernanceAction.AddGrant, role: await RoleAsync(bridge, Specialist), permission: Permission);
        var before = await bridge.SnapshotAsync();
        Assert.Equal(GovernanceOutcome.Denied, await bridge.ExecuteAsync(bridge.Original, request));
        Assert.Equal(before, await bridge.SnapshotAsync());
        Assert.Equal(GovernanceOutcome.Accepted, await bridge.ExecuteAsync(replacement, request));
        Assert.Equal(revision + 1, (await bridge.ObserveAsync()).AuthorityRevision);
    }

    /// <summary>Looks up an independently named reserved role without importing the runtime permission catalogue.</summary>
    private static async Task<string> RoleAsync(GovernanceHttpBridge bridge, string name)
    {
        await using var scope = bridge.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Roles.Where(row => row.Name == name)
            .Select(row => row.Id).SingleAsync(TestContext.Current.CancellationToken);
    }
}
