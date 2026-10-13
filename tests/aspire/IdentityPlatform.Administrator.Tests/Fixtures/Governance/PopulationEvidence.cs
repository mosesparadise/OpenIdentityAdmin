using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Uses the documented v1 permission contract and detached digest snapshots rather than the production catalogue as its oracle.</summary>
internal static class PopulationEvidence
{
    // Independent literals from specs/008-administrator-foundation/data-model.md, not AdministratorPermissions.Bundles.
    private static readonly string[] PlatformGrants =
    [
        "applications.read", "applications.write", "applications.credentials", "applications.disable",
        "scopes.read", "scopes.write", "audiences.read", "audiences.write", "configuration.read", "configuration.evaluate",
        "users.read", "users.write", "users.disable", "users.unlock", "users.recovery", "roles.read", "roles.write",
        "roles.permissions", "grants.read", "grants.revoke", "sessions.read", "sessions.revoke", "audit.read", "keys.read",
        "administrators.assign"
    ];

    /// <summary>Proves that the real native enrollment starts with the exact complete independently specified bundle and one usable actor.</summary>
    internal static async Task RequireCompleteAsync(GovernanceScenario scenario)
    {
        var result = await scenario.ReadAsync();
        RequireCompleteGrants(result.Permissions);
        Assert.Equal(scenario.ActorId, Assert.Single(result.UsableAdministrators));
    }

    /// <summary>Requires exact finite grants; count equality alone cannot hide a substituted or missing permission.</summary>
    internal static void RequireCompleteGrants(IEnumerable<string> grants) =>
        Assert.Equal(PlatformGrants.Order(StringComparer.Ordinal), grants.Order(StringComparer.Ordinal));

    /// <summary>Removes only recovery verifiers as an explicit fixture operation, demonstrating that population is independent of them.</summary>
    internal static async Task RemoveRecoveryCodesAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await database.Set<AdministratorRecoveryCode>().Where(row => row.UserId == scenario.ActorId).ExecuteDeleteAsync(token);
        Assert.False(await database.Set<AdministratorRecoveryCode>().AnyAsync(row => row.UserId == scenario.ActorId, token));
    }
}
