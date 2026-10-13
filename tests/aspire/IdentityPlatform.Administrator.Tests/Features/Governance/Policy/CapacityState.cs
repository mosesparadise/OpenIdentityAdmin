using System.Globalization;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Captures independently constructed command targets and complete expected lock ownership.</summary>
internal sealed record CapacityState(GovernanceRequest Request, string Witness, string[] Accounts, string[] AccountRoles)
{
    private const int AffectedOnlyNominees = 124;
    private const int UsableAccounts = 2;
    private const int ExplicitTarget = 1;
    private const int ReservedRoles = 4;
    private const int ActorPlatformMembership = 1;
    private const string NomineePrefix = "capacity-nominee-";
    private const string CustomPrefix = "capacity-custom-";
    private const string UnrelatedClaim = "capacity.unrelated";
    private const string IdFormat = "N";

    /// <summary>Creates two native usable accounts and a disabled nominee before independently saturating one bound.</summary>
    internal static async Task<CapacityState> CreateAsync(GovernanceScenario scenario, CapacityCase test)
    {
        var witness = await GovernanceAccounts.AddAsync(scenario);
        var target = await GovernanceAccounts.AddAsync(scenario, usable: false, platformMember: false);
        var platform = await GrantState.RoleIdAsync(scenario, GrantCases.Platform);
        var identity = await GrantState.RoleIdAsync(scenario, GrantCases.Identity);
        var accounts = new List<string> { scenario.ActorId, witness, target };
        await using (var scope = scenario.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            switch (test.Kind)
            {
                case CapacityKind.Accounts:
                    AddAccounts(database, scenario.ActorId, witness, platform, identity, accounts, test.Count);
                    break;
                case CapacityKind.Memberships:
                    AddCustomRoles(database, test.Count - ActorPlatformMembership, scenario.ActorId);
                    break;
                case CapacityKind.Roles:
                    AddCustomRoles(database, test.Count - ReservedRoles, null);
                    break;
                case CapacityKind.Claims:
                    await AddClaimsAsync(database, identity, test.Count);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(test));
            }
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            await RequireOccupancyAsync(database, scenario.ActorId, identity, accounts, platform, test);
        }
        var request = await scenario.RequestAsync(GovernanceAction.AssignBundle, target, identity);
        return new CapacityState(request, witness, accounts.Order(StringComparer.Ordinal).ToArray(), [platform, identity]);
    }

    /// <summary>Overlaps both usable accounts across roles while keeping the explicit target outside either discovered member set.</summary>
    private static void AddAccounts(PlatformDbContext database, string actor, string witness, string platform, string affected,
        List<string> accounts, int count)
    {
        database.UserRoles.AddRange(Membership(actor, affected), Membership(witness, affected));
        for (var index = 0; index < AffectedOnlyNominees; index++)
        {
            accounts.Add(AddNominee(database, affected));
        }
        var witnesses = count - UsableAccounts - ExplicitTarget - AffectedOnlyNominees;
        for (var index = 0; index < witnesses; index++)
        {
            accounts.Add(AddNominee(database, platform));
        }
    }

    /// <summary>Seeds inert, coherent native/product metadata; no password, factor or session authority is fabricated.</summary>
    private static string AddNominee(PlatformDbContext database, string role)
    {
        var suffix = Guid.NewGuid().ToString(IdFormat);
        var user = new PlatformUser
        {
            Id = suffix,
            UserName = NomineePrefix + suffix,
            NormalizedUserName = (NomineePrefix + suffix).ToUpperInvariant(),
            IsEnabled = false,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString(IdFormat),
            ConcurrencyStamp = Guid.NewGuid().ToString(IdFormat)
        };
        database.Users.Add(user);
        database.Add(new AdministratorEnrollment { UserId = user.Id });
        database.Add(new AdministratorAuthentication { UserId = user.Id });
        database.UserRoles.Add(Membership(user.Id, role));
        return user.Id;
    }

    /// <summary>Creates distinct harmless custom roles, optionally counting them as real actor memberships.</summary>
    private static void AddCustomRoles(PlatformDbContext database, int count, string? actor)
    {
        for (var index = 0; index < count; index++)
        {
            var name = CustomPrefix + index.ToString(CultureInfo.InvariantCulture);
            var role = new IdentityRole(name) { NormalizedName = name.ToUpperInvariant() };
            database.Roles.Add(role);
            if (actor is not null)
            {
                database.UserRoles.Add(Membership(actor, role.Id));
            }
        }
    }

    /// <summary>Retains the independently expected Identity bundle and pads only unrelated nonpermission claim rows.</summary>
    private static async Task AddClaimsAsync(PlatformDbContext database, string role, int count)
    {
        var existing = await database.RoleClaims.AsNoTracking().Where(row => row.RoleId == role)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GrantCases.Expected(GrantCases.Identity).Order(StringComparer.Ordinal),
            existing.Select(row => row.ClaimValue!).Order(StringComparer.Ordinal));
        Assert.All(existing, row => Assert.Equal(GrantCases.ClaimType, row.ClaimType));
        for (var index = existing.Length; index < count; index++)
        {
            database.RoleClaims.Add(new IdentityRoleClaim<string>
            {
                RoleId = role,
                ClaimType = UnrelatedClaim,
                ClaimValue = index.ToString(CultureInfo.InvariantCulture)
            });
        }
    }

    /// <summary>Checks persisted fixture occupancy independently of the production bounded reader or configured constants.</summary>
    private static async Task RequireOccupancyAsync(PlatformDbContext database, string actor, string role,
        List<string> accounts, string platform, CapacityCase test)
    {
        var token = TestContext.Current.CancellationToken;
        var actual = test.Kind switch
        {
            CapacityKind.Accounts => accounts.Count,
            CapacityKind.Memberships => await database.UserRoles.CountAsync(row => row.UserId == actor, token),
            CapacityKind.Roles => await database.Roles.CountAsync(token),
            CapacityKind.Claims => await database.RoleClaims.CountAsync(row => row.RoleId == role, token),
            _ => throw new ArgumentOutOfRangeException(nameof(test))
        };
        Assert.Equal(test.Count, actual);
        var members = await database.UserRoles.AsNoTracking().Where(row => row.RoleId == platform || row.RoleId == role)
            .Select(row => row.UserId).ToArrayAsync(token);
        var expected = accounts.ToHashSet(StringComparer.Ordinal);
        var union = members.Append(actor).Append(accounts.Single(id => !members.Contains(id, StringComparer.Ordinal)))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(expected.SetEquals(union));
        Assert.Equal(1, await database.Users.CountAsync(row => row.Id != actor && row.PasswordHash != null, token));
    }

    /// <summary>Builds only the native membership key pair used in explicit synthetic occupancy.</summary>
    private static IdentityUserRole<string> Membership(string user, string role) => new() { UserId = user, RoleId = role };
}
