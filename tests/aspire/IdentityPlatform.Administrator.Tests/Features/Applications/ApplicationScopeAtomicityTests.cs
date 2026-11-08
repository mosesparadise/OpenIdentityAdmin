using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves new native assignments cannot escape post-save corruption or failed audit with a falsely successful result.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopeAtomicityTests(BootstrapFixture fixture)
{
    private const string VerificationBypass = "application-scope-permission-verification-bypass";
    private const string Permission = "scp:orders.read";

    /// <summary>Production exact permission and catalogue snapshot checks reject deliberate persisted substitution and roll back the complete transaction.</summary>
    [Theory]
    [InlineData("PostgreSql", 0, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 1, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 2, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", 0, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 1, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 2, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedAssignment_When_NativePermissionOrPolicyIsSubstituted_Then_VerificationRejectsAndAllRowsRollBack(string provider, int change)
    {
        var fault = new ApplicationScopeSaveFault(change);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var scopes = await ApplicationScopeEvidence.SeedAsync(test);
        var created = await test.ExecuteAsync(test.Create());
        var id = created.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        var catalogue = await CatalogueMutationEvidence.SnapshotAsync(test);
        fault.ApplicationId = id;
        fault.ScopeId = scopes[1].Id;
        fault.AudienceId = scopes[1].AudienceId;
        var error = await Record.ExceptionAsync(() => test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(id, created.Version!, new([ApplicationScopeEvidence.Orders]))!));
        Assert.NotNull(fault.PersistedPermissions);
        Assert.Contains(Permission, fault.PersistedPermissions);
        Assert.True(error is InvalidOperationException, VerificationBypass);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, id)).Digest);
        Assert.Equal(catalogue, await CatalogueMutationEvidence.SnapshotAsync(test));
        fault.ApplicationId = null;
        Assert.Equal(200, (await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(id, created.Version!, new([ApplicationScopeEvidence.Orders]))!)).Status);
    }

    /// <summary>A successful native UPDATE followed by save failure or actual provider-denied audit INSERT leaves the original application version reusable.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedAssignment_When_SaveOrAuditFails_Then_ExactOriginalStateAndVersionSurvive(string provider, bool denyAudit)
    {
        var fault = new ApplicationUpdateFault(!denyAudit);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        await ApplicationScopeEvidence.SeedAsync(test);
        var created = await test.ExecuteAsync(test.Create());
        var id = created.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        var command = ApplicationMutationCommand.AssignScopes(id, created.Version!, new([ApplicationScopeEvidence.Orders]))!;
        fault.ApplicationId = id;
        fault.Armed = true;
        await ApplicationUpdateTests.FailAsync(test, command, denyAudit, fault);
        Assert.NotNull(fault.Persisted);
        Assert.NotEqual(before.StableNativeDigest, fault.Persisted.StableNativeDigest);
        Assert.Equal(before.SecretDigest, fault.Persisted.SecretDigest);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, id)).Digest);
        fault.Armed = false;
        Assert.Equal(200, (await test.ExecuteAsync(command)).Status);
    }
}
