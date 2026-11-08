using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Requires assigned applications to remain remediable without changing native permissions during durable revocation.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantAssignedScopeTests(BootstrapFixture fixture)
{
    private const string Reason = "security";
    private const string GuidFormat = "D";
    private const string Revoked = "revoked";

    /// <summary>Current disabled assigned profiles admit and finish actual native grant cleanup while retaining every application field and assignment.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DisabledAssignedApplication_When_GrantCleanupRuns_Then_NativeGrantsAreRevokedAndExactPermissionsSurvive(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await ApplicationScopeEvidence.SeedAsync(test.Fixture);
        var assignment = await test.Fixture.ExecuteAsync(ApplicationMutationCommand.AssignScopes(test.Id, test.Application.Version!, new([ApplicationScopeEvidence.Orders]))!);
        Assert.Equal(200, assignment.Status);
        await test.SeedAsync(test.Id, 1, 1);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        var intent = GrantIntent.Create(test.Id, assignment.Version!, Guid.NewGuid().ToString(GuidFormat), new(Reason, true))!;
        var job = await test.AdmitAsync(intent);
        Assert.True(await test.BatchAsync(job.Id));
        Assert.True(await test.BatchAsync(job.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.ReadAsync(job.Id)).State);
        var native = await test.StatusesAsync(test.Id);
        Assert.Equal(Revoked, Assert.Single(native.Authorizations));
        Assert.Equal(Revoked, Assert.Single(native.Tokens));
        var after = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(before.Policy.Revision, after.Policy.Revision);
        Assert.False(after.Policy.Enabled);
        (await GrantAuditWitness.ReadAsync(test, job.Id)).Require();
    }
}
