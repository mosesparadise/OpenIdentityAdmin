using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Changes test-owned persisted platform authority while proving a legitimate overlapping role cannot repair its shared bundle.</summary>
internal static class PopulationAuthority
{
    private const string PlatformRole = "PlatformAdministrator";
    private const string OverlappingRole = "Auditor";
    private const string PermissionType = "identityplatform.permission";
    private const string OverlappingPermission = "applications.read";

    /// <summary>Removes either exact platform membership or a platform-only claim after independently proving the overlapping grant exists.</summary>
    internal static async Task<bool> TryApplyAsync(GovernanceScenario scenario, string condition)
    {
        if (condition is not (PopulationCases.MissingPlatformMembership or PopulationCases.MissingPlatformGrantWithOverlap))
        {
            return false;
        }
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var role = await database.Roles.SingleAsync(row => row.Name == PlatformRole, token);
        if (condition == PopulationCases.MissingPlatformMembership)
        {
            Assert.Equal(1, await database.UserRoles.Where(row => row.UserId == scenario.ActorId && row.RoleId == role.Id).ExecuteDeleteAsync(token));
        }
        else
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = await users.FindByIdAsync(scenario.ActorId);
            Assert.NotNull(user);
            Assert.True((await users.AddToRoleAsync(user, OverlappingRole)).Succeeded);
            var overlappingId = await database.Roles.Where(row => row.Name == OverlappingRole).Select(row => row.Id).SingleAsync(token);
            Assert.Equal(1, await database.RoleClaims.CountAsync(row => row.RoleId == overlappingId && row.ClaimType == PermissionType &&
                row.ClaimValue == OverlappingPermission, token));
            Assert.Equal(1, await database.RoleClaims.Where(row => row.RoleId == role.Id && row.ClaimType == PermissionType &&
                row.ClaimValue == OverlappingPermission).ExecuteDeleteAsync(token));
        }
        return true;
    }
}
