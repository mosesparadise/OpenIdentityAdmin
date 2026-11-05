using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises real provider-observed contention between independently composed API roots and complete winner/loser state.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationLifecycleRaceTests(BootstrapFixture fixture)
{
    private const string DuplicateDisable = "disable";
    private const string DuplicateEnable = "enable";
    private const string Edit = "edit";
    private const string Replace = "replace";
    private const string Opposite = "opposite";
    private const string Maintenance = "maintenance";
    private const string Edited = "Concurrent presentation update";
    private const string Callback = "https://concurrent.example.test/callback";
    private const string Unsupported = "Unsupported lifecycle contender.";
    private const int Contenders = 2;
    private const int Updated = 200;
    private static readonly int[] Stale = [412];
    private static readonly int[] OppositeLoser = [409, 412];

    /// <summary>Both writers wait on the actual database singleton before release; exactly one effective transition/update and its audit can commit.</summary>
    [Theory]
    [InlineData("PostgreSql", DuplicateDisable, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", DuplicateEnable, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Edit, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Replace, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Opposite, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", DuplicateDisable, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", DuplicateEnable, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Edit, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Replace, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Opposite, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentApiRoots_When_LifecycleAndUpdateCommandsActuallyContend_Then_OnlyExactWinnerStateAndAuditCommit(string provider, string contender)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var peer = test.Peer(provider);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, contender == DuplicateEnable);
        var id = current.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        var enabled = contender == DuplicateEnable;
        var first = ApplicationMutationCommand.Lifecycle(id, current.Version!, new(Maintenance, true), enabled)!;
        var second = Second(test, current, contender, first);
        var warm = ApplicationMutationCommand.Lifecycle(id, ApplicationVersion.Create(id, before.Policy.Revision + 1), new(Maintenance, true), enabled)!;
        Assert.Equal(412, (await test.ExecuteOnAsync(peer, warm, Guid.NewGuid(), test.Token)).Status);
        var correlations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var lease = test.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var requests = new[] { test.ExecuteOnAsync(test.Api, first, correlations[0], test.Token), test.ExecuteOnAsync(peer, second, correlations[1], test.Token) };
        var outcomes = await ApplicationUpdateTests.ReleaseAndJoinAsync(held, () => held.ObserveWaitersAsync(lease, Contenders), requests);
        var winner = Array.FindIndex(outcomes, result => result.Status == Updated);
        Assert.Single(outcomes, result => result.Status == Updated);
        Assert.Contains(outcomes[1 - winner].Status, contender == Opposite ? OppositeLoser : Stale);
        Assert.Null(outcomes[1 - winner].Application);
        Assert.Null(outcomes[1 - winner].Credential);
        Assert.Null(outcomes[1 - winner].Version);
        var commands = new[] { first, second };
        var after = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        await RequireWinnerAsync(test, before, after, commands[winner], outcomes[winner], correlations[winner]);
        Assert.Equal(412, (await test.ExecuteAsync(commands[1 - winner])).Status);
        Assert.Equal(after.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, id)).Digest);
    }

    /// <summary>Constructs independently different inputs against the same original version without changing target identity.</summary>
    private static ApplicationMutationCommand Second(ApplicationMutationFixture test,
        IdentityPlatform.Admin.Api.Features.Applications.ApplicationMutationResult current, string contender, ApplicationMutationCommand first) => contender switch
        {
            DuplicateDisable or DuplicateEnable => first,
            Edit => ApplicationMutationCommand.Edit(current.Application!.Id, current.Version!, new(Edited, [Callback]))!,
            Replace => test.Replace(current),
            Opposite => ApplicationMutationCommand.Lifecycle(current.Application!.Id, current.Version!, new(Maintenance, true), true)!,
            _ => throw new InvalidOperationException(Unsupported)
        };

    /// <summary>Uses the independent action-specific oracle and rejects credentials or response metadata on stale peers.</summary>
    private static async Task RequireWinnerAsync(ApplicationMutationFixture test, ApplicationUpdateSnapshot before, ApplicationUpdateSnapshot after,
        ApplicationMutationCommand winner, IdentityPlatform.Admin.Api.Features.Applications.ApplicationMutationResult result, Guid correlation)
    {
        if (winner.Kind is ApplicationMutationKind.Disable or ApplicationMutationKind.Enable)
        {
            ApplicationLifecycleOracle.Require(test, before, after, winner.Kind == ApplicationMutationKind.Enable, correlation);
            ApplicationLifecycleOracle.Output(result, after);
        }
        else
        {
            await ApplicationUpdateOracle.RequireAsync(test, before, winner, result, correlation);
        }
    }
}
