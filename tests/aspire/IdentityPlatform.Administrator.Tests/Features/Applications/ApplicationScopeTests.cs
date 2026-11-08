using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises explicit native assignments and unchanged ordinary editor behavior on both real providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopeTests(BootstrapFixture fixture)
{
    private const string Audit = "ApplicationScopesAssigned";
    private const string Target = "Application";
    private const string ScopePrefix = "scp:";
    private const string Edited = "Edited with assignments";
    private const string Maintenance = "maintenance";

    /// <summary>Assignment, unchanged resubmission and empty removal each commit one version/audit while preserving every other native field and credential.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_EligibleScopes_When_AssignedReassignedAndRemoved_Then_OnlyExactPermissionsVersionAndAuditChange(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await ApplicationScopeEvidence.SeedAsync(test);
        var current = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, current.Status);
        var id = current.Application!.Id;
        var native = await ApplicationScopeEvidence.ReadAsync(test, id);
        var original = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        foreach (var names in new[] { new[] { ApplicationScopeEvidence.Orders, ApplicationScopeEvidence.Email }, new[] { ApplicationScopeEvidence.Email, ApplicationScopeEvidence.Orders }, Array.Empty<string>() })
        {
            var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
            var correlation = Guid.NewGuid();
            var command = ApplicationMutationCommand.AssignScopes(id, current.Version!, new(names))!;
            current = await test.ExecuteOnAsync(test.Api, command, correlation, test.Token);
            Assert.Equal(200, current.Status);
            Assert.Equal(ApplicationVersion.Create(id, before.Policy.Revision + 1), current.Version);
            Assert.Equal(names.Order(StringComparer.Ordinal), current.Scopes!.Scopes);
            Assert.Null(current.Credential);
            var after = await ApplicationScopeEvidence.ReadAsync(test, id);
            Assert.Equal(native.Stable, after.Stable);
            Assert.Equal(native.Permissions.Concat(names.Select(name => ScopePrefix + name)).Order(StringComparer.Ordinal), after.Permissions);
            var snapshot = await ApplicationUpdateSnapshot.ReadAsync(test, id);
            var expectedPolicy = System.Text.Json.JsonSerializer.SerializeToNode(original.Policy)!.AsObject();
            expectedPolicy[nameof(snapshot.Policy.Revision)] = snapshot.Policy.Revision;
            expectedPolicy[nameof(snapshot.Policy.ObservedAt)] = test.Clock.Now.ToUnixTimeSeconds();
            Assert.Equal(expectedPolicy.ToJsonString(), System.Text.Json.JsonSerializer.SerializeToNode(snapshot.Policy)!.ToJsonString());
            await CatalogueMutationEvidence.RequireAuditAsync(test, correlation, Audit, Target, id, test.Clock.Now.ToUnixTimeSeconds());
            Assert.Equal(412, (await test.ExecuteAsync(command)).Status);
            Assert.Equal(snapshot.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, id)).Digest);
        }
    }

    /// <summary>Existing edit/replacement/lifecycle paths retain assignments even after selected policy is disabled, and explicit removal remains available.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DisabledAssignedScope_When_OrdinaryEditorActionsCommit_Then_AssignmentIsPreservedAndRemovable(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var scopes = await ApplicationScopeEvidence.SeedAsync(test);
        var current = await test.ExecuteAsync(test.Create());
        var id = current.Application!.Id;
        current = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(id, current.Version!, new([ApplicationScopeEvidence.Email]))!);
        Assert.Equal(200, current.Status);
        var email = scopes[0];
        Assert.Equal(200, (await CatalogueMutationTests.ExecuteAsync(test, CatalogueIntent.Edit(email.Id, email.Version,
            new IdentityPlatform.Core.Features.Catalogue.ScopeInput(email.Name, email.DisplayName, email.Kind, false, email.ConsentText, null, email.Disclosure))!)).Status);
        foreach (var replace in new[] { false, true })
        {
            var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
            var command = replace ? test.Replace(current) : ApplicationMutationCommand.Edit(id, current.Version!, new(Edited, [ApplicationMutationFixture.Redirect]))!;
            var correlation = Guid.NewGuid();
            current = await test.ExecuteOnAsync(test.Api, command, correlation, test.Token);
            await ApplicationUpdateOracle.RequireAsync(test, before, command, current, correlation);
        }
        foreach (var enabled in new[] { false, true })
        {
            var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
            current = await test.ExecuteAsync(ApplicationMutationCommand.Lifecycle(id, current.Version!, new(Maintenance, true), enabled)!);
            Assert.Equal(200, current.Status);
            Assert.Equal(before.NativeDigest, (await ApplicationUpdateSnapshot.ReadAsync(test, id)).NativeDigest);
        }
        current = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(id, current.Version!, new([]))!);
        Assert.Equal(200, current.Status);
        Assert.Empty(current.Scopes!.Scopes);
    }
}
