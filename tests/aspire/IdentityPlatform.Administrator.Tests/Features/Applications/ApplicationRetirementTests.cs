using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves terminal admission, exact replay, immutable history and permanent mutation denial on both real providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementTests(BootstrapFixture fixture)
{
    private const int DisabledTerminalRevision = 3;

    private const string Reason = "security";
    private const string GuidFormat = "D";
    private const int Conflict = 409;
    private const int Stale = 412;
    private const string Edited = "Forbidden retired edit";
    private const string Callback = "https://retired.example.test/callback";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Admission preserves native credentials/history, binds its own original Submit and rejects changed replay input without additional rows.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "retirement-replay-intent")]
    public async Task Given_ActiveApplication_When_RetiredAndReplayed_Then_OneTerminalReceiptPreservesNativeIdentityAndOriginalApproval(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var original = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        var receipt = await test.ReceiptAsync();
        var terminal = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        Assert.Equal(original.NativeDigest, terminal.NativeDigest);
        Assert.True(terminal.Policy.Retired);
        Assert.False(terminal.Policy.Enabled);
        Assert.Equal(2, terminal.Policy.Revision);
        Assert.Equal(1, receipt.OriginalRevision);
        Assert.Equal(2, receipt.RetiredRevision);
        Assert.Equal(86400, receipt.ReplayUntil - receipt.RetiredAt);
        Assert.Equal(Guid.Parse(accepted.Job.Id), receipt.GrantJobId);
        var frozen = await test.SnapshotAsync();
        var replay = await test.RetireAsync(intent);
        Assert.Equal(accepted, replay);
        foreach (var changed in new[] { intent with { RequestKey = Guid.NewGuid() }, intent with { Reason = ApplicationLifecycleReason.Maintenance }, intent with { Version = accepted.ApplicationVersion } })
        {
            Assert.Equal(Conflict, (await ApplicationRetirementCausalWitness.ReplayAsync(test, changed, accepted, frozen)).Status);
            Assert.Equal(frozen, await test.SnapshotAsync());
        }
        await using var scope = test.Fixture.Scope();
        var approval = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationGrantApproval>().AsNoTracking().SingleAsync(test.Token);
        Assert.Equal(receipt.GrantApprovalId, approval.Id);
        Assert.Equal(GrantApprovalAction.Submit, approval.Action);
        Assert.Equal(receipt.ScopedKeyHash, approval.ScopedKeyHash);
        Assert.Equal(receipt.RequestHash, approval.RequestHash);
    }

    /// <summary>After revocation succeeds the registration stays terminal, its client ID remains reserved and no ordinary write can produce another version.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "retirement-terminal-write")]
    public async Task Given_RetiredApplication_When_CleanupFinishesAndOrdinaryMutationsAreAttempted_Then_AllRemainClosedAndClientIdReserved(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        await test.Grants.SeedAsync(test.Id, 1, 1);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.Grants.ReadAsync(accepted.Job.Id)).State);
        var before = await test.SnapshotAsync();
        var expiry = test.Fixture.Clock.Now.AddHours(1).ToString(ExpiryFormat, System.Globalization.CultureInfo.InvariantCulture);
        var commands = new[]
        {
            ApplicationMutationCommand.Edit(test.Id, accepted.ApplicationVersion, new(Edited, [Callback]))!,
            ApplicationMutationCommand.Replace(test.Id, accepted.ApplicationVersion, new(true, expiry), test.Fixture.Clock.Now)!,
            ApplicationMutationCommand.Lifecycle(test.Id, accepted.ApplicationVersion, new(Reason, true), true)!,
            ApplicationMutationCommand.Lifecycle(test.Id, accepted.ApplicationVersion, new(Reason, true), false)!,
            ApplicationCloneAtomicityTests.Clone(test.Fixture, test.Id, accepted.ApplicationVersion),
            test.Fixture.Create()
        };
        foreach (var command in commands)
        {
            Assert.Equal(Conflict, await ApplicationRetirementCausalWitness.MutationAsync(test, command));
            Assert.Equal(before, await test.SnapshotAsync());
        }
        var submit = GrantIntent.Create(test.Id, accepted.ApplicationVersion, intent.RequestKey.ToString(GuidFormat), new(Reason, true))!;
        Assert.Equal(Conflict, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.Grants.AdmitAsync(submit))).Status);
        var replay = await test.RetireAsync(intent);
        Assert.Equal(accepted.ApplicationVersion, replay.ApplicationVersion);
        Assert.Equal(accepted.RetiredAt, replay.RetiredAt);
        Assert.Equal("succeeded", replay.Job.State);
        Assert.Equal(before, await test.SnapshotAsync());
    }

    /// <summary>Any preexisting incomplete job blocks the whole retirement; completed ordinary history is preserved and a distinct new job is admitted.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OrdinaryGrantHistory_When_RetirementIsRequested_Then_IncompleteWorkBlocksAndCompletedWorkIsNeverAdopted(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var disabled = await test.Fixture.ExecuteAsync(ApplicationMutationCommand.Lifecycle(test.Id, test.Application.Version!, new(Reason, true), false)!);
        Assert.Equal(200, disabled.Status);
        var ordinary = GrantIntent.Create(test.Id, disabled.Version!, Guid.NewGuid().ToString(GuidFormat), new(Reason, true))!;
        var prior = await test.Grants.AdmitAsync(ordinary);
        var intent = test.Intent() with
        {
            Version = disabled.Version!
        };
        var before = await test.SnapshotAsync();
        Assert.Equal(Conflict, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent))).Status);
        Assert.Equal(before, await test.SnapshotAsync());
        Assert.True(await test.Grants.BatchAsync(prior.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.Grants.ReadAsync(prior.Id)).State);
        var accepted = await test.RetireAsync(intent);
        Assert.NotEqual(prior.Id, accepted.Job.Id);
        Assert.Equal(ApplicationVersion.Create(test.Id, DisabledTerminalRevision), accepted.ApplicationVersion);
        before = await test.SnapshotAsync();
        Assert.Equal(Conflict, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.Grants.AdmitAsync(ordinary))).Status);
        Assert.Equal(before, await test.SnapshotAsync());
    }

    /// <summary>Stale original preconditions reject admission without terminal policy, grant or receipt changes.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ChangedApplication_When_OriginalVersionRetires_Then_PreconditionFailurePreservesAllRows(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var intent = test.Intent();
        Assert.Equal(200, (await test.Fixture.ExecuteAsync(ApplicationMutationCommand.Edit(test.Id, test.Application.Version!, new(Edited, [Callback]))!)).Status);
        var before = await test.SnapshotAsync();
        Assert.Equal(Stale, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent))).Status);
        Assert.Equal(before, await test.SnapshotAsync());
    }
}
