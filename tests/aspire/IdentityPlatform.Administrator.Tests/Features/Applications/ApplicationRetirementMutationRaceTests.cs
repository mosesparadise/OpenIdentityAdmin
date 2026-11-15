using System.Globalization;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires real lock-wait observations for every mutable source boundary against permanent retirement in both commit orders.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementMutationRaceTests(BootstrapFixture fixture)
{
    private const string Edit = "edit";
    private const string Clone = "clone";
    private const string Replace = "replace";
    private const string Scopes = "scopes";
    private const string Display = "Retirement race edit";
    private const string Callback = "https://retirement-race.example/callback";
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const int Accepted = 202;
    private const int Created = 201;
    private const int ClonedPolicies = 2;
    private const int Ok = 200;
    private const int Stale = 412;

    /// <summary>Retirement-first denies every ordinary stale mutation; source-first invalidates retirement except a clone that does not revise its source.</summary>
    [Theory]
    [InlineData("PostgreSql", "edit", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "edit", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "clone", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "clone", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "replace", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "replace", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "scopes", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "scopes", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", "edit", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "edit", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "clone", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "clone", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "replace", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "replace", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "scopes", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "scopes", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConcurrentSourceMutation_When_RetirementCommitOrderIsObserved_Then_NoPostRetirementWriteOrCloneCanCommit(string provider, string operation, bool retirementFirst)
    {
        var probe = new GrantLifecycleRaceProbe(winner: true);
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider, probe);
        await using var peer = test.Fixture.Peer(provider);
        var command = Command(test, operation);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        probe.Armed = true;
        var first = retirementFirst ? RetireAsync(test, test.Fixture.Api) : MutateAsync(test, test.Fixture.Api, command);
        var second = await ApplicationCloneRaceTests.QueueAsync(test.Fixture, probe, first,
            () => retirementFirst ? MutateAsync(test, peer, command) : RetireAsync(test, peer));
        probe.RequireOutcome();
        var retired = retirementFirst ? await first : second;
        var mutated = retirementFirst ? second : await first;
        Assert.Equal(retirementFirst ? Stale : operation == Clone ? Created : Ok, mutated.Status);
        Assert.Equal(retirementFirst || operation == Clone ? Accepted : Stale, retired.Status);
        var after = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        Assert.Equal(retired.Status == Accepted, after.Policy.Retired);
        Assert.Equal(retired.Status != Accepted, after.Policy.Enabled);
        if (retirementFirst || operation == Clone)
        {
            Assert.Equal(before.NativeDigest, after.NativeDigest);
            Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
            Assert.Equal(retired.Acceptance!.Job.Id, (await test.ReceiptAsync()).GrantJobId.ToString());
        }
        await RequireRowsAsync(test, operation == Clone && !retirementFirst, retired.Status == Accepted);
    }

    /// <summary>Checks actual persisted clone provenance and terminal receipt presence, rather than relying on finite HTTP statuses.</summary>
    private static async Task RequireRowsAsync(ApplicationRetirementTestContext test, bool clone, bool retired)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(clone ? 1 : 0, await database.Set<ApplicationCloneAuditDetail>().CountAsync(test.Token));
        Assert.Equal(retired ? 1 : 0, await database.Set<ApplicationRetirement>().CountAsync(test.Token));
        Assert.Equal(clone ? ClonedPolicies : 1, await database.Set<ApplicationPolicy>().CountAsync(test.Token));
    }

    /// <summary>Freezes a current original-version command before either independent transaction begins.</summary>
    private static ApplicationMutationCommand Command(ApplicationRetirementTestContext test, string operation) => operation switch
    {
        Clone => ApplicationCloneAtomicityTests.Clone(test.Fixture, test.Id, test.Application.Version!),
        Edit => ApplicationMutationCommand.Edit(test.Id, test.Application.Version!, new(Display, [Callback]))!,
        Replace => ApplicationMutationCommand.Replace(test.Id, test.Application.Version!, new(true, test.Fixture.Clock.Now.AddDays(1).ToString(DateFormat, CultureInfo.InvariantCulture)), test.Fixture.Clock.Now)!,
        Scopes => ApplicationMutationCommand.AssignScopes(test.Id, test.Application.Version!, new([]))!,
        _ => throw new InvalidOperationException()
    };

    /// <summary>Captures only expected stale denial; unexpected ownership or storage failures remain test failures.</summary>
    private static async Task<Outcome> RetireAsync(ApplicationRetirementTestContext test, ServiceProvider node)
    {
        try
        {
            return new(Accepted, await test.RetireAsync(test.Intent(), node));
        }
        catch (ApplicationHttpRejected error) when (error.Status == Stale) { return new(error.Status, null); }
    }

    /// <summary>Uses the real mutation owner on the selected independent root and discards one-time secret material from outcome evidence.</summary>
    private static async Task<Outcome> MutateAsync(ApplicationRetirementTestContext test, ServiceProvider node, ApplicationMutationCommand command) =>
        new((await test.Fixture.ExecuteOnAsync(node, command, Guid.NewGuid(), test.Token)).Status, null);

    /// <summary>Retains only safe finite status and bounded retirement metadata across joined transaction ownership.</summary>
    private sealed record Outcome(int Status, ApplicationRetirementAcceptance? Acceptance);
}
