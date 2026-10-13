using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Persists individually invalid native authority shapes without bypassing provider constraints.</summary>
internal static class MalformedGrants
{
    private const string Padding = " ";
    private const string Wildcard = "*";
    private const string DormantRole = "DormantAuditRole";
    private const string RoleForeignKey = "FK_AspNetUserRoles_AspNetRoles_RoleId";
    private const string UserForeignKey = "FK_AspNetUserRoles_AspNetUsers_UserId";
    private const string InvalidAliasFailure = "The exact native alias foreign-key constraint was not the observed failure.";

    /// <summary>Returns false only when PostgreSQL rejects the selected alias through its independently verified foreign key.</summary>
    internal static async Task<bool> PersistAsync(GovernanceScenario scenario, string provider, string variant)
    {
        if (variant is GrantCases.RoleIdAlias or GrantCases.UserIdAlias)
        {
            return await PersistAliasAsync(scenario, provider, variant);
        }
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var platform = await database.Roles.SingleAsync(row => row.Name == GrantCases.Platform, token);
        if (variant is GrantCases.RoleNameCase or GrantCases.RoleNamePadding or GrantCases.NormalizedRoleAlias or GrantCases.AmbiguousReservedIdentity)
        {
            await ChangeRoleAsync(database, platform, variant);
        }
        else if (variant == GrantCases.OutOfBundleGrant)
        {
            await GrantState.SetMembershipsAsync(scenario, GrantCases.Application);
            var role = await database.Roles.SingleAsync(row => row.Name == GrantCases.Application, token);
            AddPermission(database, role.Id, GrantCases.UserRead);
        }
        else if (variant == GrantCases.CustomRolePermission)
        {
            var custom = GrantState.AddCustomMembership(database, scenario.ActorId);
            AddPermission(database, custom.Id, GrantCases.UserRead);
        }
        else
        {
            await ChangeClaimAsync(database, platform.Id, variant);
        }
        await database.SaveChangesAsync(token);
        return true;
    }

    /// <summary>Creates one reserved identity violation; the ambiguous case first frees the competing normalized unique value.</summary>
    private static async Task ChangeRoleAsync(PlatformDbContext database, IdentityRole role, string variant)
    {
        switch (variant)
        {
            case GrantCases.RoleNameCase:
                role.Name = GrantCases.Platform.ToLowerInvariant();
                break;
            case GrantCases.RoleNamePadding:
                role.Name = GrantCases.Platform + Padding;
                break;
            case GrantCases.NormalizedRoleAlias:
                role.NormalizedName = GrantCases.Platform.ToLowerInvariant();
                break;
            case GrantCases.AmbiguousReservedIdentity:
                var auditor = await database.Roles.SingleAsync(row => row.Name == GrantCases.Auditor, TestContext.Current.CancellationToken);
                auditor.Name = DormantRole;
                auditor.NormalizedName = DormantRole.ToUpperInvariant();
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
                role.NormalizedName = GrantCases.Auditor.ToUpperInvariant();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant));
        }
    }

    /// <summary>Changes exactly one role claim except the explicit duplicate, whose original row remains persisted.</summary>
    private static async Task ChangeClaimAsync(PlatformDbContext database, string roleId, string variant)
    {
        var claim = await database.RoleClaims.SingleAsync(row => row.RoleId == roleId && row.ClaimType == GrantCases.ClaimType &&
            row.ClaimValue == GrantCases.UserRead, TestContext.Current.CancellationToken);
        switch (variant)
        {
            case GrantCases.ClaimTypeCase:
                claim.ClaimType = GrantCases.ClaimType.ToUpperInvariant();
                break;
            case GrantCases.ClaimTypePadding:
                claim.ClaimType = GrantCases.ClaimType + Padding;
                break;
            case GrantCases.ClaimValueCase:
                claim.ClaimValue = GrantCases.UserRead.ToUpperInvariant();
                break;
            case GrantCases.ClaimValuePadding:
                claim.ClaimValue = GrantCases.UserRead + Padding;
                break;
            case GrantCases.Wildcard:
                claim.ClaimValue = Wildcard;
                break;
            case GrantCases.UnknownGrant:
                claim.ClaimValue = GrantCases.Unknown;
                break;
            case GrantCases.DuplicateRoleGrant:
                AddPermission(database, roleId, GrantCases.UserRead);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant));
        }
    }

    /// <summary>Adds a real native permission row without using any production grant default as an expected value.</summary>
    private static void AddPermission(PlatformDbContext database, string roleId, string value) =>
        database.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = roleId, ClaimType = GrantCases.ClaimType, ClaimValue = value });

    /// <summary>Exercises deterministic trailing-space aliases against actual collation and foreign keys, restoring state on schema denial.</summary>
    private static async Task<bool> PersistAliasAsync(GovernanceScenario scenario, string provider, string variant)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var original = await database.UserRoles.AsNoTracking().SingleAsync(token);
        Assert.Equal(1, await database.UserRoles.Where(row => row.UserId == scenario.ActorId).ExecuteDeleteAsync(token));
        database.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = original.UserId + (variant == GrantCases.UserIdAlias ? Padding : string.Empty),
            RoleId = original.RoleId + (variant == GrantCases.RoleIdAlias ? Padding : string.Empty)
        });
        if (provider == GrantCases.PostgreSql)
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(token));
            var expectedConstraint = variant == GrantCases.RoleIdAlias ? RoleForeignKey : UserForeignKey;
            Assert.True(failure.InnerException is Npgsql.PostgresException native &&
                native.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation && native.ConstraintName == expectedConstraint, InvalidAliasFailure);
            return false;
        }
        Assert.Equal(GrantCases.SqlServer, provider);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }
}
