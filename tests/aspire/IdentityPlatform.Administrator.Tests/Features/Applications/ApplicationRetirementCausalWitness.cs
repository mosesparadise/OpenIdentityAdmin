using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Emits exact mutation markers only after an actual successful forbidden commit is independently observed.</summary>
internal static class ApplicationRetirementCausalWitness
{
    internal const string Dual = "retirement-dual-permission-bypass";
    internal const string Authority = "retirement-current-authority-bypass";
    internal const string Replay = "retirement-replay-intent-bypass";
    internal const string Terminal = "retirement-terminal-write-bypass";
    internal const string Receipt = "retirement-receipt-persistence-bypass";
    internal const string Policy = "retirement-policy-persistence-bypass";
    private const string Permission = "grants.revoke";
    private const string Event = "ApplicationRetired";
    private const string CorruptHash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
    private const int Ok = 200;

    /// <summary>Requires actual terminal admission plus independently absent grant permission before marking an authority bypass.</summary>
    internal static async Task<ApplicationHttpRejected> AuthorityAsync(ApplicationRetirementTestContext test, ApplicationRetirementIntent intent, string marker)
    {
        ApplicationRetirementAcceptance? accepted = null;
        var failure = await Record.ExceptionAsync(async () => accepted = await test.RetireAsync(intent));
        if (failure is null)
        {
            await AcceptedAsync(test, Assert.IsType<ApplicationRetirementAcceptance>(accepted));
            await using var scope = test.Fixture.Scope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().RoleClaims.AnyAsync(row => row.ClaimValue == Permission, test.Token));
            Assert.Fail(marker);
        }
        return Assert.IsType<ApplicationHttpRejected>(failure);
    }

    /// <summary>A changed intent must actually return the original result and preserve all rows before the replay marker can be emitted.</summary>
    internal static async Task<ApplicationHttpRejected> ReplayAsync(ApplicationRetirementTestContext test, ApplicationRetirementIntent changed,
        ApplicationRetirementAcceptance original, string frozen)
    {
        ApplicationRetirementAcceptance? accepted = null;
        var failure = await Record.ExceptionAsync(async () => accepted = await test.RetireAsync(changed));
        if (failure is null)
        {
            Assert.Equal(original, accepted);
            Assert.Equal(frozen, await test.SnapshotAsync());
            Assert.NotEqual(Guid.Parse(original.RequestKey), changed.RequestKey);
            Assert.Fail(Replay);
        }
        return Assert.IsType<ApplicationHttpRejected>(failure);
    }

    /// <summary>Requires an actual edited native row, advanced revision and immutable terminal receipt before marking forbidden ordinary writing.</summary>
    internal static async Task<int> MutationAsync(ApplicationRetirementTestContext test, ApplicationMutationCommand command)
    {
        var before = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        var receipt = await test.ReceiptAsync();
        var result = await test.Fixture.ExecuteAsync(command);
        if (result.Status == Ok)
        {
            Assert.Equal(ApplicationMutationKind.Edit, command.Kind);
            var after = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
            Assert.Equal(command.DisplayName, after.Display);
            Assert.Equal(command.Redirects, after.Redirects);
            Assert.True(after.Policy.Retired);
            Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
            Assert.Equal(before.Audits.Length + 1, after.Audits.Length);
            Assert.Equal(ApplicationUpdateSnapshot.Hash(receipt), ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync()));
            Assert.Fail(Terminal);
        }
        return result.Status;
    }

    /// <summary>Requires the deliberate saved corruption and linked accepted work to commit before emitting either atomicity marker.</summary>
    internal static async Task<InvalidOperationException> AtomicAsync(ApplicationRetirementTestContext test, ApplicationRetirementIntent intent,
        ApplicationRetirementSaveFault fault, bool receipt)
    {
        var before = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        ApplicationRetirementAcceptance? accepted = null;
        var failure = await Record.ExceptionAsync(async () => accepted = await test.RetireAsync(intent));
        Assert.True(fault.Fired);
        if (failure is null)
        {
            await AcceptedAsync(test, Assert.IsType<ApplicationRetirementAcceptance>(accepted), retired: receipt);
            var persisted = await test.ReceiptAsync();
            Assert.Equal(receipt ? CorruptHash : intent.RequestHash(), persisted.RequestHash);
            Assert.Equal(before.NativeDigest, (await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id)).NativeDigest);
            Assert.Fail(receipt ? Receipt : Policy);
        }
        return Assert.IsType<InvalidOperationException>(failure);
    }

    /// <summary>Checks actual original receipt, terminal policy, queued job, Submit approval and both audit associations through fresh scopes.</summary>
    internal static async Task AcceptedAsync(ApplicationRetirementTestContext test, ApplicationRetirementAcceptance accepted, bool retired = true)
    {
        var receipt = await test.ReceiptAsync();
        var policy = (await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id)).Policy;
        Assert.Equal(retired, policy.Retired);
        Assert.False(policy.Enabled);
        Assert.Equal(receipt.RetiredRevision, policy.Revision);
        Assert.Equal(receipt.ApplicationId, accepted.ApplicationId);
        Assert.Equal(receipt.GrantJobId, Guid.Parse(accepted.Job.Id));
        var history = await GrantAuditWitness.ReadAsync(test.Grants, accepted.Job.Id);
        history.Require();
        Assert.Equal(receipt.GrantApprovalId, Assert.Single(history.Approvals).Id);
        Assert.Single(history.Audits);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Id == receipt.AuditId, test.Token);
        Assert.Equal(Event, audit.Event);
        Assert.Equal(test.Id, audit.TargetId);
        Assert.Equal(receipt.ActorId, audit.ActorId);
    }
}
