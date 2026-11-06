using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises explicit continuation through genuine API/BFF authentication after actual current-permission loss stopped the worker.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantHttpRecoveryTests(BootstrapFixture fixture)
{
    private const string Permission = "grants.revoke";
    private const string PermissionClaim = "identityplatform.permission";
    private const string Reason = "security";
    private const string Queued = "queued";
    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string GuidFormat = "D";

    /// <summary>Stale job versions fail before a fresh confirmation; the same accepted continuation replays across surfaces with one new immutable approval.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PermissionStoppedJob_When_FreshContinuationAndReplayCrossHttpsSurfaces_Then_OneNewApprovalResumesOriginalJob(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var (id, version) = await GrantHttpTests.DisabledAsync(test);
        using var response = await test.GrantAsync(id, Body, version, Guid.NewGuid().ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
        await StopAndRestoreAsync(test, accepted.Job.Id);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await GrantAuditWitness.ReadAsync(database, Guid.Parse(accepted.Job.Id), TestContext.Current.CancellationToken);
        before.Require();
        Assert.Equal(GrantJobState.Stopped, before.Job.State);
        Assert.Equal(GrantJobFailure.Authority, before.Job.Failure);
        await ContinueAsync(test, id, version, accepted, before.Job.Revision);
        var after = await GrantAuditWitness.ReadAsync(database, before.Job.Id, TestContext.Current.CancellationToken);
        after.Require();
        after.Preserves(before);
        Assert.Equal(2, after.Approvals.Length);
        Assert.Equal(before.Job.Revision + 1, after.Job.Revision);
        Assert.NotEqual(before.Job.EffectiveApprovalId, after.Job.EffectiveApprovalId);
        Assert.True(await BatchAsync(test, accepted.Job.Id));
        var complete = await GrantAuditWitness.ReadAsync(database, before.Job.Id, TestContext.Current.CancellationToken);
        complete.Require();
        Assert.Equal(GrantJobState.Succeeded, complete.Job.State);
    }

    /// <summary>Sends stale and accepted confirmations on both fixed HTTPS routes while retaining the exact accepted key and payload for replay.</summary>
    private static async Task ContinueAsync(ApplicationHttpFixture test, string id, string version, GrantAcceptance accepted, long revision)
    {
        var key = Guid.NewGuid().ToString(GuidFormat);
        var stale = JsonSerializer.Serialize(new GrantRevocation(Reason, true, accepted.Job.Version), JsonSerializerOptions.Web);
        var body = JsonSerializer.Serialize(new GrantRevocation(Reason, true, ApplicationVersion.Create(accepted.Job.Id, revision)), JsonSerializerOptions.Web);
        foreach (var direct in new[] { false, true })
        {
            using var denied = await test.GrantAsync(id, stale, version, Guid.NewGuid().ToString(GuidFormat), direct, accepted.Job.Id);
            await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.PreconditionFailed);
            using var continued = await test.GrantAsync(id, body, version, key, direct, accepted.Job.Id);
            Assert.Equal(HttpStatusCode.Accepted, continued.StatusCode);
            var replay = (await continued.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
            Assert.Equal(accepted.Job.Id, replay.Job.Id);
            Assert.Equal(Queued, replay.Job.State);
        }
    }

    /// <summary>Removes actual persisted grants, runs the real worker stop, and restores only those claims without manufacturing a continuation.</summary>
    internal static async Task StopAndRestoreAsync(ApplicationHttpFixture test, string id)
    {
        var token = TestContext.Current.CancellationToken;
        string[] roles;
        await using (var scope = test.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            roles = await database.RoleClaims.Where(row => row.ClaimValue == Permission).Select(row => row.RoleId).ToArrayAsync(token);
            Assert.NotEmpty(roles);
            Assert.Equal(roles.Length, await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(token));
        }
        Assert.True(await BatchAsync(test, id));
        await using (var scope = test.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            foreach (var role in roles)
            {
                database.RoleClaims.Add(new()
                {
                    RoleId = role,
                    ClaimType = PermissionClaim,
                    ClaimValue = Permission
                });
            }
            await database.SaveChangesAsync(token);
        }
        Assert.False(await BatchAsync(test, id));
    }

    /// <summary>Uses fresh Worker ownership against the actual HTTPS fixture's provider state without a browser receipt.</summary>
    private static Task<bool> BatchAsync(ApplicationHttpFixture test, string id) => GrantBatchOperation.ExecuteAsync(
        test.Environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(id), test.Environment.Issuer.Account.Journey.Clock,
        TestContext.Current.CancellationToken);
}
