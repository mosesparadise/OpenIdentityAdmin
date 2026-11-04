using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Independent approved bootstrap role and permission expectations.</summary>
internal static class BootstrapBundles
{
    private const string ApplicationAdministrator = "ApplicationAdministrator";
    private const string ApplicationPermissions = "applications.read applications.write applications.credentials applications.disable scopes.read scopes.write audiences.read audiences.write configuration.read configuration.evaluate";
    private const string IdentityAdministrator = "IdentityAdministrator";
    private const string IdentityPermissions = "users.read users.write users.disable users.unlock users.recovery roles.read roles.write roles.permissions grants.read grants.revoke sessions.read sessions.revoke";
    private const string Auditor = "Auditor";
    private const string AuditorPermissions = "applications.read scopes.read audiences.read users.read roles.read grants.read sessions.read audit.read configuration.read keys.read";
    private const string PlatformAdministrator = "PlatformAdministrator";
    private const string AssignAdministrators = "administrators.assign";
    private const string PermissionClaimType = "identityplatform.permission";

    /// <summary>Checks the independently specified role, claim and administrator-binding contract.</summary>
    internal static async Task AssertBundlesAsync(PlatformDbContext database, string userId)
    {
        // Independent approved contract values: do not derive the oracle from production constants.
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ApplicationAdministrator] = ApplicationPermissions,
            [IdentityAdministrator] = IdentityPermissions,
            [Auditor] = AuditorPermissions
        };
        expected[PlatformAdministrator] = string.Join(' ', expected.Values.SelectMany(value => value.Split(' ')).Append(AssignAdministrators).Distinct(StringComparer.Ordinal));
        var roles = await database.Roles.ToListAsync(Token);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), roles.Select(value => value.Name!).Order(StringComparer.Ordinal));
        foreach (var role in roles)
        {
            var claims = await database.RoleClaims.Where(value => value.RoleId == role.Id).ToListAsync(Token);
            Assert.All(claims, claim => Assert.Equal(PermissionClaimType, claim.ClaimType));
            Assert.Equal(expected[role.Name!].Split(' ').Order(StringComparer.Ordinal), claims.Select(value => value.ClaimValue!).Order(StringComparer.Ordinal));
        }
        var binding = await database.UserRoles.SingleAsync(value => value.UserId == userId, Token);
        Assert.Equal(roles.Single(value => value.Name == PlatformAdministrator).Id, binding.RoleId);
    }
}
