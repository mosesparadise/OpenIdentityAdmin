using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Reconciles several jobs without weakening the original single-job orphan-audit oracle or substituting synthetic authority.</summary>
internal static class GrantReplayEvidence
{
    private const string Prefix = "ApplicationGrantRevocation";

    /// <summary>Enrolls and signs in a separate administrator through the existing native password/MFA journey.</summary>
    internal static async Task<ApiIdentity> OtherActorAsync(GrantTestContext test)
    {
        var account = test.Fixture.Scenario.Account;
        var actor = await GovernanceAccounts.AddAsync(account);
        test.Fixture.Clock.Now = DateTimeOffset.UtcNow;
        var session = await GovernanceAccounts.LoginAsync(account, actor);
        return await test.Fixture.NativeIdentityAsync(actor, session.SessionId);
    }

    /// <summary>Checks the complete isolated grant ledger, including unassociated audit intents, before deriving per-job histories.</summary>
    internal static async Task<GrantAuditWitness[]> HistoriesAsync(GrantTestContext test, params string[] ids)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var expected = ids.Select(Guid.Parse).Order().ToArray();
        var jobs = await database.Set<ApplicationGrantJob>().AsNoTracking().Select(row => row.Id).ToArrayAsync(test.Token);
        Assert.Equal(expected, jobs.Order());
        var details = await database.Set<ApplicationGrantAudit>().AsNoTracking().ToArrayAsync(test.Token);
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event.StartsWith(Prefix)).ToArrayAsync(test.Token);
        Assert.Equal(details.Select(row => row.AuditId).Order(), audits.Select(row => row.Id).Order());
        var result = new List<GrantAuditWitness>();
        foreach (var id in ids)
        {
            var raw = await GrantAuditWitness.ReadAsync(database, Guid.Parse(id), test.Token);
            var history = raw with
            {
                Audits = [.. audits.Where(row => raw.Details.Any(detail => detail.AuditId == row.Id))]
            };
            history.Require();
            result.Add(history);
        }
        Assert.Equal(details.Length, result.Sum(row => row.Details.Length));
        Assert.Equal(await database.Set<ApplicationGrantApproval>().CountAsync(test.Token), result.Sum(row => row.Approvals.Length));
        return [.. result];
    }

    /// <summary>Requires exact original job and history preservation after another scope is accepted.</summary>
    internal static void Unchanged(GrantAuditWitness before, GrantAuditWitness after)
    {
        after.Preserves(before);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Job), ApplicationUpdateSnapshot.Hash(after.Job));
        Assert.Equal(before.Approvals.Length, after.Approvals.Length);
        Assert.Equal(before.Details.Length, after.Details.Length);
        Assert.Equal(before.Audits.Length, after.Audits.Length);
    }

    /// <summary>Hashes all grant/native/audit state from a fresh scope for denied requests and successful idempotent replays.</summary>
    internal static async Task<string> SnapshotAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        return await GrantBoundaryEvidence.SnapshotAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), test.Id);
    }

    /// <summary>Excludes expected new grant audit intents while retaining every native application, credential, policy and lifecycle field.</summary>
    internal static async Task<string> ConfigurationAsync(GrantTestContext test, string id)
    {
        var value = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, id);
        return ApplicationUpdateSnapshot.Hash(new
        {
            value.NativeDigest,
            value.Policy,
            value.LifecycleAudits
        });
    }
}
