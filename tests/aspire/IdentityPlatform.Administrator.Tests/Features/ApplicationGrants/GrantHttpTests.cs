using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises native issuer sign-in and independent real HTTPS API/BFF authority for durable acceptance and protected progress.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantHttpTests(BootstrapFixture fixture)
{
    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string ChangedBody = "{\"reason\":\"maintenance\",\"confirm\":true}";
    private const string GuidFormat = "D";
    private const string Separator = "/";
    private const string Suffix = "/grant-revocations";
    private const string Foreign = "https://foreign.example.test";
    private const string InvalidProof = "invalid-proof";
    private const string Permission = "grants.revoke";
    private const string ReadPermission = "applications.read";
    private static readonly string?[] InvalidKeys = [null, "bad", "00000000-0000-0000-0000-000000000000", "F27BF8D2-84A7-4FC6-A6CE-309521A0976B"];
    private static readonly string[] InvalidBodies = ["{}", "{\"reason\":\"security\",\"confirm\":false}",
        "{\"reason\":\"security\",\"reason\":\"maintenance\",\"confirm\":true}", "{\"reason\":\"security\",\"confirm\":true,\"jobVersion\":\"*\"}"];

    /// <summary>Requires durable 202, exact same-key replay with one approval, current safe reads and successful empty reconciliation on both surfaces.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineAdministrator_When_GrantAcceptanceIsReplayedThroughBothSurfaces_Then_OneJobAndApprovalAreDurable(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var (id, version) = await DisabledAsync(test);
        var key = Guid.NewGuid().ToString(GuidFormat);
        GrantAcceptance? first = null;
        foreach (var direct in new[] { false, true })
        {
            using var response = await test.GrantAsync(id, Body, version, key, direct);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var accepted = (await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
            first ??= accepted;
            Assert.Equal(first.Job.Id, accepted.Job.Id);
            Assert.Equal(key, accepted.RequestKey);
            Assert.Equal(accepted.Job.Version, response.Headers.ETag!.Tag);
            var path = (direct ? ApplicationHttpFixture.ApiCollection : ApplicationHttpFixture.Collection) + Separator + id + Suffix;
            Assert.Equal(path + Separator + accepted.Job.Id, response.Headers.Location!.OriginalString);
            using var read = direct ? await test.DirectGetAsync(path) : await test.Browser.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(first.Job.Id, Assert.Single((await read.Content.ReadFromJsonAsync<GrantJobPage>(TestContext.Current.CancellationToken))!.Items).Id);
            using var changed = await test.GrantAsync(id, ChangedBody, version, key, direct);
            await ApplicationHttpTests.ProblemAsync(changed, HttpStatusCode.Conflict);
        }
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<ApplicationGrantJob>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await database.Set<ApplicationGrantApproval>().CountAsync(TestContext.Current.CancellationToken));
        Assert.True(await GrantBatchOperation.ExecuteAsync(test.Environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(first!.Job.Id), test.Environment.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken));
        Assert.Equal(GrantJobState.Succeeded, (await database.Set<ApplicationGrantJob>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).State);
    }

    /// <summary>Rejects key/body/precondition aliases, missing native proof, foreign origins and absent bearer before any durable grant job exists.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineSession_When_GrantInputOrProofIsInvalid_Then_NoJobIsAdmitted(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var (id, version) = await DisabledAsync(test);
        var key = Guid.NewGuid().ToString(GuidFormat);
        foreach (var direct in new[] { false, true })
        {
            foreach (var invalid in InvalidKeys)
            {
                using var response = await test.GrantAsync(id, Body, version, invalid, direct);
                await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.BadRequest);
            }
            foreach (var body in InvalidBodies)
            {
                using var response = await test.GrantAsync(id, body, version, key, direct);
                await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.BadRequest);
            }
            using var missing = await test.GrantAsync(id, Body, null, key, direct);
            await ApplicationHttpTests.ProblemAsync(missing, HttpStatusCode.PreconditionRequired);
            using var anonymous = await test.GrantAsync(id, Body, version, key, direct, authenticated: false);
            await ApplicationHttpTests.ProblemAsync(anonymous, direct ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest);
        }
        using var foreign = await test.GrantAsync(id, Body, version, key, origin: Foreign);
        await ApplicationHttpTests.ProblemAsync(foreign, HttpStatusCode.BadRequest);
        using var invalidProof = await test.GrantAsync(id, Body, version, key, csrf: InvalidProof);
        await ApplicationHttpTests.ProblemAsync(invalidProof, HttpStatusCode.BadRequest);
        await using var scope = test.Scope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationGrantJob>().ToArrayAsync(TestContext.Current.CancellationToken));
        await GrantTargetHttpEvidence.RequireAsync(test, id, version);
    }

    /// <summary>Current persisted permissions govern both exact and recent status; holding job references or previously valid cookies never supplies read authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Permission, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ReadPermission, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Permission, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ReadPermission, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_CurrentReadPermissionIsRemoved_Then_ApiAndBrowserStatusAreForbidden(string provider, string permission)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var (id, version) = await DisabledAsync(test);
        using var response = await test.GrantAsync(id, Body, version, Guid.NewGuid().ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
        await using var scope = test.Scope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        foreach (var direct in new[] { false, true })
        {
            var path = (direct ? ApplicationHttpFixture.ApiCollection : ApplicationHttpFixture.Collection) + Separator + id + Suffix;
            foreach (var target in new[] { path, path + Separator + accepted.Job.Id })
            {
                using var denied = direct ? await test.DirectGetAsync(target) : await test.Browser.GetAsync(target, TestContext.Current.CancellationToken);
                await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.Forbidden);
            }
        }
    }

    /// <summary>Creates and disables a managed registration using the genuine BFF write journey before grant testing begins.</summary>
    internal static async Task<(string Id, string Version)> DisabledAsync(ApplicationHttpFixture test)
    {
        using var response = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(response);
        using var disabled = await test.UpdateAsync(created.Application.Id, Body, response.Headers.ETag!.Tag, enabled: false);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        return (created.Application.Id, disabled.Headers.ETag!.Tag);
    }
}
