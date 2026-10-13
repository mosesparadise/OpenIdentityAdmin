using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Changes exactly one native intent version and independently checks all other supplied dimensions remain equal.</summary>
internal static class GovernanceVersionDamage
{
    internal const string Authority = "authority-revision";
    internal const string UserStamp = "native-user-stamp";
    internal const string RoleStamp = "native-role-stamp";
    internal const string Enrollment = "enrollment-revision";

    /// <summary>Requires production discovery and locked intent validation to accept current versions without verifying or committing a command.</summary>
    internal static async Task RequireCurrentAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        await using var operation = await GovernanceOperation.OpenAsync(scenario, request);
        Assert.Same(request, operation.State.Request);
    }

    /// <summary>Persists one native version change through a separate scope after the original intent was prepared.</summary>
    internal static async Task ChangeAsync(GovernanceScenario scenario, GovernanceRequest request, string variant)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString();
        var changed = variant switch
        {
            Authority => await database.Set<AdministratorGovernance>()
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.AuthorityRevision, row => row.AuthorityRevision + 1), token),
            UserStamp => await database.Users.Where(row => row.Id == request.UserId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.ConcurrencyStamp, stamp), token),
            RoleStamp => await database.Roles.Where(row => row.Id == request.RoleId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.ConcurrencyStamp, stamp), token),
            Enrollment => await database.Set<AdministratorEnrollment>().Where(row => row.UserId == request.UserId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), token),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        Assert.Equal(1, changed);
    }

    /// <summary>Compares each explicit version independently, avoiding an assertion that would pass when unrelated values also changed.</summary>
    internal static void RequireOnlySelectedVersionChanged(GovernanceRequest original, GovernanceRequest fresh, string variant)
    {
        Assert.Equal(original.Action, fresh.Action);
        Assert.Equal(original.UserId, fresh.UserId);
        Assert.Equal(original.RoleId, fresh.RoleId);
        Assert.Equal(original.Permission, fresh.Permission);
        Assert.Equal(variant == Authority, original.Versions.AuthorityRevision != fresh.Versions.AuthorityRevision);
        Assert.Equal(variant == UserStamp, original.Versions.UserStamp != fresh.Versions.UserStamp);
        Assert.Equal(variant == RoleStamp, original.Versions.RoleStamp != fresh.Versions.RoleStamp);
        Assert.Equal(variant == Enrollment, original.Versions.EnrollmentRevision != fresh.Versions.EnrollmentRevision);
    }
}
