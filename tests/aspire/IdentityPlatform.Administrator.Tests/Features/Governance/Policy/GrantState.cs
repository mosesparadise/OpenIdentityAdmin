using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Provides explicit native fixture mutations and independent assertions, never a substituted governance policy.</summary>
internal static class GrantState
{
    private const int SqlServerDuplicateKey = 2627;
    private const string MembershipPrimaryKey = "PK_AspNetUserRoles";
    private const string InvalidDuplicate = "The native membership primary-key constraint was not the observed failure.";

    /// <summary>Replaces fixture membership with precisely the requested persisted role IDs using an independent native scope.</summary>
    internal static async Task SetMembershipsAsync(GovernanceScenario scenario, params string[] names)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await database.UserRoles.Where(row => row.UserId == scenario.ActorId).ExecuteDeleteAsync(token);
        var roles = await database.Roles.Where(role => names.Contains(role.Name!)).ToArrayAsync(token);
        Assert.Equal(names.Length, roles.Length);
        database.UserRoles.AddRange(roles.Select(role => new IdentityUserRole<string> { UserId = scenario.ActorId, RoleId = role.Id }));
        await database.SaveChangesAsync(token);
    }

    /// <summary>Resolves one exact persisted reserved role without deriving expected grants from its catalogue.</summary>
    internal static async Task<string> RoleIdAsync(GovernanceScenario scenario, string name)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await database.Roles.AsNoTracking().Where(role => role.Name == name).Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Compares complete ordinal sets rather than merely their cardinalities or a known subset.</summary>
    internal static void RequirePermissions(IEnumerable<string> expected, GovernanceObservation actual) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Permissions.Order(StringComparer.Ordinal));

    /// <summary>Proves specialist permissions depend on current native enable and enrollment state, with positive restoration controls.</summary>
    internal static async Task RequireSpecialistStateAsync(GovernanceScenario scenario, string bundle)
    {
        await SetEnabledAsync(scenario, false);
        Assert.Empty((await scenario.ReadAsync()).Permissions);
        await SetEnabledAsync(scenario, true);
        RequirePermissions(GrantCases.Expected(bundle), await scenario.ReadAsync());
        await SetSuspendedAsync(scenario, true);
        Assert.Empty((await scenario.ReadAsync()).Permissions);
        await SetSuspendedAsync(scenario, false);
        RequirePermissions(GrantCases.Expected(bundle), await scenario.ReadAsync());
    }

    /// <summary>Changes only the native fixture enable flag, without claiming an administrative command or resurrection journey ran.</summary>
    private static async Task SetEnabledAsync(GovernanceScenario scenario, bool enabled)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Users.Where(row => row.Id == scenario.ActorId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsEnabled, enabled), TestContext.Current.CancellationToken));
    }

    /// <summary>Changes only the fixture suspension flag to exercise current credential eligibility independently of membership.</summary>
    private static async Task SetSuspendedAsync(GovernanceScenario scenario, bool suspended)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<AdministratorEnrollment>().Where(row => row.UserId == scenario.ActorId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Suspended, suspended), TestContext.Current.CancellationToken));
    }

    /// <summary>Persists exactly one supplied role claim through the runtime native context.</summary>
    internal static async Task AddClaimAsync(GovernanceScenario scenario, string role, string type, string value)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var roleId = await database.Roles.Where(row => row.Name == role).Select(row => row.Id).SingleAsync(TestContext.Current.CancellationToken);
        database.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = roleId, ClaimType = type, ClaimValue = value });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Deletes exactly one granted permission in a separately committed native context.</summary>
    internal static async Task RemoveClaimAsync(GovernanceScenario scenario, string role, string permission)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var roleId = await database.Roles.Where(row => row.Name == role).Select(row => row.Id).SingleAsync(token);
        Assert.Equal(1, await database.RoleClaims.Where(row => row.RoleId == roleId && row.ClaimType == GrantCases.ClaimType && row.ClaimValue == permission)
            .ExecuteDeleteAsync(token));
    }

    /// <summary>Builds only the non-authoritative source selected by the native case identity.</summary>
    internal static async Task AddNonAuthorityAsync(GovernanceScenario scenario, IHttpContextAccessor accessor, string variant)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        switch (variant)
        {
            case GrantCases.UserClaim:
                database.UserClaims.Add(new IdentityUserClaim<string> { UserId = scenario.ActorId, ClaimType = GrantCases.ClaimType, ClaimValue = GrantCases.UserRead });
                break;
            case GrantCases.PrincipalClaim:
                Assert.Same(accessor, scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>());
                Assert.True(accessor.HttpContext!.User.HasClaim(GrantCases.ClaimType, GrantCases.UserRead));
                break;
            case GrantCases.CustomRole:
                AddCustomMembership(database, scenario.ActorId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant));
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Seeds an ordinary custom role with an unrelated claim, never a recognized permission-bearing role.</summary>
    internal static IdentityRole AddCustomMembership(PlatformDbContext database, string actorId)
    {
        var role = new IdentityRole(GrantCases.CustomRoleName) { NormalizedName = GrantCases.CustomRoleName.ToUpperInvariant() };
        database.Roles.Add(role);
        database.UserRoles.Add(new IdentityUserRole<string> { UserId = actorId, RoleId = role.Id });
        database.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = role.Id, ClaimType = GrantCases.UnrelatedType, ClaimValue = GrantCases.UnrelatedValue });
        return role;
    }

    /// <summary>Inserts a detached duplicate so rejection occurs in the real database rather than EF identity tracking.</summary>
    internal static async Task RequireDuplicateConstraintAsync(GovernanceScenario scenario, string provider)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await database.UserRoles.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        database.UserRoles.Add(new IdentityUserRole<string> { UserId = row.UserId, RoleId = row.RoleId });
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.True(provider == GrantCases.PostgreSql
            ? failure.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: MembershipPrimaryKey }
            : failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlServerDuplicateKey }, InvalidDuplicate);
    }
}
