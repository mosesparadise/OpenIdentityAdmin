using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Applies one named fixture-only corruption without relaxing schema constraints or invoking a substitute policy.</summary>
internal static class PopulationDamage
{
    private const string UnknownCorruption = "Unknown population corruption.";
    private const string Pending = "Pending";
    private const string Consumed = "Consumed";
    private const string Recovery = "Recovery";
    private const string Unknown = "NotAnEnrollmentState";
    private const int InvalidRevision = -1;
    private const int FutureSeconds = 3600;
    private const int ExhaustedFailures = 5;

    /// <summary>Persists one approved condition, leaving real constraint exceptions intact for separate native classification.</summary>
    internal static async Task ApplyAsync(GovernanceScenario scenario, string condition)
    {
        if (condition == PopulationCases.InvalidCurrentTime)
        {
            scenario.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(InvalidRevision);
            return;
        }
        if (await PopulationMaterial.TryApplyAsync(scenario, condition) || await PopulationAuthority.TryApplyAsync(scenario, condition))
        {
            return;
        }
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        if (condition is PopulationCases.MissingEnrollment or PopulationCases.MissingAuthentication)
        {
            await DeleteReferencedRowAsync(database, scenario.ActorId, condition, token);
            return;
        }
        var user = await database.Users.SingleAsync(row => row.Id == scenario.ActorId, token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == scenario.ActorId, token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == scenario.ActorId, token);
        var now = scenario.Journey.Clock.Now;
        Assert.True(DamageNative(database, user, condition, now) || DamageEnrollment(enrollment, condition) ||
            DamageAuthentication(authentication, condition, now.ToUnixTimeSeconds()), UnknownCorruption);
        await database.SaveChangesAsync(token);
    }

    /// <summary>Attempts native deletion without tracked dependent fixup, so actual restrictive database constraints supply the rejection.</summary>
    private static async Task DeleteReferencedRowAsync(PlatformDbContext database, string userId, string condition, CancellationToken token)
    {
        if (condition == PopulationCases.MissingEnrollment)
        {
            await database.Set<AdministratorEnrollment>().Where(row => row.UserId == userId).ExecuteDeleteAsync(token);
        }
        else
        {
            await database.Set<AdministratorAuthentication>().Where(row => row.UserId == userId).ExecuteDeleteAsync(token);
        }
    }

    /// <summary>Changes one native requirement; deleting a user retains any product orphan permitted by the actual schema.</summary>
    private static bool DamageNative(PlatformDbContext database, PlatformUser user, string condition, DateTimeOffset now)
    {
        switch (condition)
        {
            case PopulationCases.MissingUser:
                database.Users.Remove(user);
                break;
            case PopulationCases.DisabledUser:
                user.IsEnabled = false;
                break;
            case PopulationCases.NativeTwoFactorFalse:
                user.TwoFactorEnabled = false;
                break;
            case PopulationCases.MissingPassword:
                user.PasswordHash = null;
                break;
            case PopulationCases.EmptySecurityStamp:
                user.SecurityStamp = string.Empty;
                break;
            case PopulationCases.NativeLockoutDisabled:
                user.LockoutEnabled = false;
                break;
            case PopulationCases.NativeLockout:
                user.LockoutEnd = now.AddSeconds(FutureSeconds);
                break;
            default:
                return false;
        }
        return true;
    }

    /// <summary>Changes one enrollment requirement; negative revisions and referenced row deletion must meet native rejection controls.</summary>
    private static bool DamageEnrollment(AdministratorEnrollment enrollment, string condition)
    {
        switch (condition)
        {
            case PopulationCases.PendingEnrollment:
                enrollment.State = Pending;
                break;
            case PopulationCases.ConsumedEnrollment:
                enrollment.State = Consumed;
                break;
            case PopulationCases.RecoveryEnrollment:
                enrollment.State = Recovery;
                break;
            case PopulationCases.UnknownEnrollment:
                enrollment.State = Unknown;
                break;
            case PopulationCases.SuspendedEnrollment:
                enrollment.Suspended = true;
                break;
            case PopulationCases.ZeroFactorGeneration:
                enrollment.FactorGeneration = 0;
                break;
            case PopulationCases.NegativeEnrollmentRevision:
                enrollment.Revision = InvalidRevision;
                break;
            default:
                return false;
        }
        return true;
    }

    /// <summary>Separates malformed bounded rows, inconsistent lockout windows, coherent active lockout and a future clock fence.</summary>
    private static bool DamageAuthentication(AdministratorAuthentication authentication, string condition, long now)
    {
        switch (condition)
        {
            case PopulationCases.ZeroAuthenticationGeneration:
                authentication.Generation = 0;
                break;
            case PopulationCases.NegativeAuthenticationRevision:
                authentication.Revision = InvalidRevision;
                break;
            case PopulationCases.NegativeFailureCount:
                authentication.Failures = InvalidRevision;
                break;
            case PopulationCases.InvalidFailureWindow:
                authentication.LockoutUntil = now + FutureSeconds;
                break;
            case PopulationCases.ProductLockout:
                authentication.Failures = ExhaustedFailures;
                authentication.LockoutUntil = now + FutureSeconds;
                break;
            case PopulationCases.ClockFence:
                authentication.ObservedAt = now + FutureSeconds;
                break;
            default:
                return false;
        }
        return true;
    }
}
