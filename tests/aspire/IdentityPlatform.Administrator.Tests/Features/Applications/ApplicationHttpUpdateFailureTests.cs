using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using static IdentityPlatform.Administrator.Tests.Features.Applications.ApplicationHttpUpdateEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises actual provider audit denial through both mapped update actions and proves exact rollback plus original-version recovery.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationHttpUpdateFailureTests(BootstrapFixture fixture)
{
    /// <summary>A real database permission outage returns only sanitized503, retaining every native/policy/audit field and allowing explicit recovery after restoration.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualAuditPermissionDenial_When_HttpsUpdateRuns_Then_ExactAggregateRollsBackAndOriginalVersionRecovers(string provider, bool replacement, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var id = created.Application.Id;
        var before = await SnapshotAsync(test, id);
        var body = replacement ? Replacement(test.Now) : Edit;
        await IssuerAuditPermission.RunAsync(test.Environment.Issuer.Account.Journey.Lease, async () =>
        {
            using var failed = await test.UpdateAsync(id, body, create.Headers.ETag!.ToString(), replacement, direct);
            await ApplicationHttpTests.ProblemAsync(failed, HttpStatusCode.ServiceUnavailable);
        });
        Assert.Equal(before.Digest, (await SnapshotAsync(test, id)).Digest);
        using var recovery = await test.UpdateAsync(id, body, create.Headers.ETag!.ToString(), replacement, direct);
        await RequireAsync(test, before, recovery, body, replacement, created.ClientSecret);
    }
}
