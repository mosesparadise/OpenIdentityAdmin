using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Prepares two actual accounts and native relationship changes for an explicit discovery-to-lock test seam.</summary>
internal sealed record GovernancePlanChange(string Actor, string Candidate, string AddedRole)
{
    internal const string AffectedMember = "new-affected-member";
    internal const string EarlierWitness = "earlier-sorting-witness";
    private const string Platform = "PlatformAdministrator";
    private const string Specialist = "IdentityAdministrator";

    /// <summary>Uses fixed account ordering without retries; only the later account initially holds the platform membership.</summary>
    internal static async Task<GovernancePlanChange> CreateAsync(GovernanceScenario scenario, string variant)
    {
        if (variant == AffectedMember)
        {
            var nominee = await GovernanceAccounts.AddAsync(scenario, false, false);
            return new GovernancePlanChange(scenario.ActorId, nominee, await GovernanceCommands.RoleAsync(scenario, Specialist));
        }
        Assert.Equal(EarlierWitness, variant);
        var other = await GovernanceAccounts.AddAsync(scenario, true, false);
        var ordered = new[] { scenario.ActorId, other }.Order(StringComparer.Ordinal).ToArray();
        var roleId = await GovernanceCommands.RoleAsync(scenario, Platform);
        await SetOnlyMemberAsync(scenario, ordered[1], roleId);
        Assert.True(StringComparer.Ordinal.Compare(ordered[0], ordered[1]) < 0);
        return new GovernancePlanChange(ordered[1], ordered[0], roleId);
    }

    /// <summary>Adds the selected native membership in an independent scope after the production plan has already been discovered.</summary>
    internal async Task ApplyAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.False(await database.UserRoles.AnyAsync(row => row.UserId == Candidate && row.RoleId == AddedRole, token));
        database.UserRoles.Add(new IdentityUserRole<string> { UserId = Candidate, RoleId = AddedRole });
        Assert.Equal(1, await database.SaveChangesAsync(token));
    }

    /// <summary>Configures the initial known platform population solely as test setup, without inventing a product onboarding operation.</summary>
    private static async Task SetOnlyMemberAsync(GovernanceScenario scenario, string actorId, string roleId)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        Assert.Equal(1, await database.UserRoles.Where(row => row.RoleId == roleId).ExecuteDeleteAsync(token));
        database.UserRoles.Add(new IdentityUserRole<string> { UserId = actorId, RoleId = roleId });
        Assert.Equal(1, await database.SaveChangesAsync(token));
        await transaction.CommitAsync(token);
    }
}
