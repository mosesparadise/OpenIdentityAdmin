using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Seeds same-transaction persisted changes after successful admission while tracked authority snapshots remain stale.</summary>
[Collection("administrator-bootstrap")]
public sealed class AdmissionDriftTests(BootstrapFixture fixture)
{
    /// <summary>Final admission must reread full source, active protected factor and current persisted permissions instead of trusting its earlier result.</summary>
    [Theory, MemberData(nameof(AdmissionCases.Drift), MemberType = typeof(AdmissionCases))]
    public async Task Given_AcceptedAdmission_When_PersistedAuthorityChangesBeforeCommit_Then_OriginalProofCannotAuthorize(string provider, string damage)
    {
        await using var scenario = await AdmissionScenario.CreateAsync(fixture, provider);
        var observed = false;
        // Observe real mutation after a positive proof so setup failure cannot count as final-authority denial.
        async Task<bool> RunAsync() => await scenario.RunAsync(async (scope, result, token) =>
        {
            Assert.Equal(ProtocolAdmissionOutcome.Allowed, result.Outcome);
            Assert.NotNull(result.Proof);
            var clock = scope.Services.GetRequiredService<CeremonyClock>();
            Assert.True(await result.Proof.IsCurrentAsync(clock, token));
            var changed = await scope.Owner.RunAsync(current => DamageAsync((PlatformDbContext)scope.Owner.Database,
                scenario.Account.ActorId, scenario.Account.Session.SessionId, damage, current), token, write: true);
            Assert.True(changed > 0);
            observed = true;
            Assert.False(await result.Proof.IsCurrentAsync(clock, token));
            Assert.Null(await result.Proof.DescribeAsync(clock, token));
            return true;
        });
        if (damage == AdmissionCases.NoGrants)
        {
            Assert.True(await RunAsync());
        }
        else
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(RunAsync);
        }
        Assert.True(observed);
        await scenario.RunAsync(async (scope, result, token) =>
        {
            Assert.NotNull(result.Proof);
            Assert.True(await result.Proof.IsCurrentAsync(scope.Services.GetRequiredService<CeremonyClock>(), token));
            return true;
        });
    }

    /// <summary>Changes only the selected persisted authority inside the already held transaction; rollback must restore the later positive control.</summary>
    private static async ValueTask<int> DamageAsync(PlatformDbContext database, string user, string session, string damage, CancellationToken token) => damage switch
    {
        AdmissionCases.Disabled => await database.Users.Where(row => row.Id == user).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), token),
        AdmissionCases.Suspended => await database.Set<AdministratorEnrollment>().Where(row => row.UserId == user)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Suspended, true), token),
        AdmissionCases.MissingFactor => await database.Set<AdministratorFactor>().Where(row => row.UserId == user).ExecuteDeleteAsync(token),
        AdmissionCases.NoGrants => await database.UserRoles.Where(row => row.UserId == user).ExecuteDeleteAsync(token),
        AdmissionCases.Authentication => await database.Set<AdministratorAuthentication>().Where(row => row.UserId == user)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), token),
        AdmissionCases.Revoked => await database.Set<AdministratorSession>().Where(row => row.PublicId == session)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), token),
        AdmissionCases.Factor => await database.Set<AdministratorEnrollment>().Where(row => row.UserId == user)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.FactorGeneration, row => row.FactorGeneration + 1), token),
        _ => throw new ArgumentOutOfRangeException(nameof(damage))
    };
}
