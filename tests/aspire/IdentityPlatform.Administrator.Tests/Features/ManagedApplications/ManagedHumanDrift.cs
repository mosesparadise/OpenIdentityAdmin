using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Changes and restores actual independently persisted account/application authority between real HTTP phases.</summary>
internal static class ManagedHumanDrift
{
    /// <summary>Applies one isolated stale authority condition while retaining exact tracked originals for explicit restoration.</summary>
    internal static async Task CheckAsync(ManagedCacheTestContext test, ManagedHumanAuthority defect, Func<Task<HttpResponseMessage>> send)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.SingleAsync(token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(token);
        var policy = await database.Set<ApplicationPolicy>().SingleAsync(token);
        var originals = database.ChangeTracker.Entries().Select(entry => (Entry: entry, Values: entry.CurrentValues.Clone())).ToArray();
        Mutate(test, defect, user, enrollment, authentication, policy);
        await database.SaveChangesAsync(token);
        try
        {
            var before = await ManagedHumanEvidence.DigestAsync(test);
            using var response = await send();
            await ManagedCacheTestContext.UnavailableAsync(response);
            Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        }
        finally
        {
            foreach (var original in originals)
            {
                original.Entry.CurrentValues.SetValues(original.Values);
            }
            await database.SaveChangesAsync(token);
        }
    }
    /// <summary>Changes only the named independent authority field; callers retain and restore complete original values.</summary>
    private static void Mutate(ManagedCacheTestContext test, ManagedHumanAuthority defect, PlatformUser user,
        AdministratorEnrollment enrollment, AdministratorAuthentication authentication, ApplicationPolicy policy)
    {
        switch (defect)
        {
            case ManagedHumanAuthority.Disabled:
                user.IsEnabled = false;
                break;
            case ManagedHumanAuthority.Suspended:
                enrollment.Suspended = true;
                break;
            case ManagedHumanAuthority.Stamp:
                user.SecurityStamp = Guid.NewGuid().ToString();
                break;
            case ManagedHumanAuthority.Authentication:
                authentication.Generation++;
                break;
            case ManagedHumanAuthority.Factor:
                enrollment.FactorGeneration++;
                break;
            case ManagedHumanAuthority.Enrollment:
                enrollment.Revision++;
                break;
            case ManagedHumanAuthority.Policy:
                policy.Revision++;
                break;
            case ManagedHumanAuthority.Credential:
                policy.CredentialExpiresAt = test.Http.Now.ToUnixTimeSeconds();
                break;
            case ManagedHumanAuthority.Locked:
                authentication.LockoutUntil = test.Http.Now.AddMinutes(1).ToUnixTimeSeconds();
                break;
        }
    }

}

/// <summary>Identifies independent current-authority invalidations, each followed by exact restoration before the next control.</summary>
internal enum ManagedHumanAuthority
{
    Disabled, Suspended, Stamp, Authentication, Factor, Enrollment, Policy, Credential, Locked
}
