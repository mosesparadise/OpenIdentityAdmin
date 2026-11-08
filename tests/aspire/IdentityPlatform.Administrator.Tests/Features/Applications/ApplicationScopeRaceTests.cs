using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Verifies shared aggregate and catalogue-deletion ordering using actual independent provider sessions.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopeRaceTests(BootstrapFixture fixture)
{
    private const string ScopePrefix = "scp:";
    private const string Edited = "Competing edit";
    private const string Callback = "https://competing.example.test/callback";
    private const string Audit = "ApplicationScopesAssigned";
    private const string Target = "Application";
    private const int Contenders = 2;
    private const string GuidFormat = "D";

    private const int Accepted = 200;

    /// <summary>Actual lock waiters compete on one version; a losing assignment or ordinary edit cannot overwrite the winner or append an audit.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentNodesAndOneVersion_When_AssignmentContendsWithAssignmentOrEdit_Then_ExactlyOneWinnerCommits(string provider, bool edit)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await ApplicationScopeEvidence.SeedAsync(test);
        var created = await test.ExecuteAsync(test.Create());
        var id = created.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        var originalNative = await ApplicationScopeEvidence.ReadAsync(test, id);
        await using var peer = test.Peer(provider);
        var first = ApplicationMutationCommand.AssignScopes(id, created.Version!, new([ApplicationScopeEvidence.Email]))!;
        var second = edit ? ApplicationMutationCommand.Edit(id, created.Version!, new(Edited, [Callback]))!
            : ApplicationMutationCommand.AssignScopes(id, created.Version!, new([ApplicationScopeEvidence.Orders]))!;
        var correlations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var lease = test.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var outcomes = await ApplicationUpdateTests.ReleaseAndJoinAsync(held, () => held.ObserveWaitersAsync(lease, Contenders),
            test.ExecuteOnAsync(test.Api, first, correlations[0], test.Token), test.ExecuteOnAsync(peer, second, correlations[1], test.Token));
        Assert.Equal([200, 412], outcomes.Select(value => value.Status).Order());
        var winner = Array.FindIndex(outcomes, value => value.Status == Accepted);
        if (winner == 1 && edit)
        {
            await ApplicationUpdateOracle.RequireAsync(test, before, second, outcomes[winner], correlations[winner]);
        }
        else
        {
            var expected = winner == 0 ? first : second;
            Assert.Equal(expected.Scopes, outcomes[winner].Scopes!.Scopes);
            var persisted = await ApplicationScopeEvidence.ReadAsync(test, id);
            Assert.Equal(originalNative.Stable, persisted.Stable);
            Assert.Equal(originalNative.Permissions.Concat(expected.Scopes.Select(name => ScopePrefix + name)).Order(StringComparer.Ordinal), persisted.Permissions);
            await CatalogueMutationEvidence.RequireAuditAsync(test, correlations[winner], Audit, Target, id, test.Clock.Now.ToUnixTimeSeconds());
        }
        Assert.Null(outcomes[1 - winner].Version);
        Assert.Null(outcomes[1 - winner].Application);
        Assert.Null(outcomes[1 - winner].Scopes);
        Assert.Null(outcomes[1 - winner].Credential);
        var after = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
        Assert.Equal(before.Audits.Length + 1, after.Audits.Length);
    }

    /// <summary>A queued deletion rereads newly assigned native permissions; the opposite completed-deletion ordering rejects a new assignment.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmittedScopeDeletion_When_AssignmentAndWorkerRunInEitherOrder_Then_NoDanglingNativePermissionCommits(string provider, bool deleteFirst)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var scopes = await ApplicationScopeEvidence.SeedAsync(test);
        var created = await test.ExecuteAsync(test.Create());
        var selected = scopes[0];
        var intent = CatalogueDeleteIntent.Create(CatalogueResource.Scope, selected.Id, selected.Version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
        var admitted = await CatalogueDeletionTest.AdmitAsync(test, intent);
        Assert.Equal(202, admitted.Status);
        var job = admitted.Job!;
        if (deleteFirst)
        {
            Assert.True(await CatalogueDeletionTest.ProcessAsync(test, job.Id));
        }
        var command = ApplicationMutationCommand.AssignScopes(created.Application!.Id, created.Version!, new([selected.Name]))!;
        Assert.Equal(deleteFirst ? 409 : 200, (await test.ExecuteAsync(command)).Status);
        if (!deleteFirst)
        {
            Assert.True(await CatalogueDeletionTest.ProcessAsync(test, job.Id));
        }
        await CatalogueDeletionTest.RequireTerminalAsync(test, job.Id, deleteFirst ? CatalogueDeletionFailure.None : CatalogueDeletionFailure.Referenced);
    }
}
