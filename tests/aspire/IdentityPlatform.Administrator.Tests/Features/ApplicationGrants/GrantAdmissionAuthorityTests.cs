using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.ApplicationGrants;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantBoundaryEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Separates current grant and MFA admission from earlier authentication rejection and from permission-free worker continuation.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantAdmissionAuthorityTests(BootstrapFixture fixture)
{
    private const int LastFreshSecond = 299;
    private const string DisablePermission = "applications.disable";
    private const string WritePermission = "applications.write";
    private const string CredentialPermission = "applications.credentials";
    private const string Reason = "security";

    /// <summary>A real saved approval cannot commit using the earlier authority observation after current permission or the original MFA lifetime changes.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_StagedAdmission_When_CurrentPermissionOrMfaEnds_Then_FinalObservationRollsBackAllWrites(string provider, bool expireMfa)
    {
        var change = new GrantAdmissionChange(expireMfa);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, change);
        await test.SeedAsync(test.Id, 1, 1);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var verified = await database.Set<AdministratorSession>().Where(row => row.PublicId == test.Fixture.Identity.Session)
            .Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Fixture.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + LastFreshSecond);
        change.Clock = test.Fixture.Clock;
        var before = await SnapshotAsync(database, test.Id);
        var permissions = await database.RoleClaims.CountAsync(row => row.ClaimValue == Permission, test.Token);
        change.Armed = true;
        var denied = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.AdmitAsync(test.Intent()));
        Assert.Equal(expireMfa ? 401 : 403, denied.Status);
        Assert.True(change.Observed);
        Assert.Equal(before, await SnapshotAsync(database, test.Id));
        Assert.Equal(permissions, await database.RoleClaims.CountAsync(row => row.ClaimValue == Permission, test.Token));
    }

    /// <summary>Existing application permissions and real bearer/cookie possession cannot accept or recover a job after only revocation permission is removed.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineAccess_When_RevocationPermissionIsRemoved_Then_NewAndReplayRequestsDenyWithoutChangingState(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var (id, version) = await GrantHttpTests.DisabledAsync(test);
        var key = Guid.NewGuid().ToString(GuidFormat);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var claims = await database.RoleClaims.AsNoTracking().Where(row => row.ClaimValue == Permission).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(claims);
        Assert.Equal(claims.Length, await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        await DeniedAsync(test, database, id, version, key);
        database.RoleClaims.AddRange(claims.Select(row => new Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>
        {
            RoleId = row.RoleId,
            ClaimType = row.ClaimType,
            ClaimValue = row.ClaimValue
        }));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == DisablePermission || row.ClaimValue == WritePermission || row.ClaimValue == CredentialPermission)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        using var allowed = await test.GrantAsync(id, Body, version, key, direct: true);
        Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        var accepted = (await allowed.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
        (await GrantAuditWitness.ReadAsync(database, Guid.Parse(accepted.Job.Id), TestContext.Current.CancellationToken)).Require();
        Assert.Equal(claims.Length, await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        await DeniedAsync(test, database, id, version, key);
        await DeniedAsync(test, database, id, version, Guid.NewGuid().ToString(GuidFormat));
        await DeniedContinuationAsync(test, database, id, version, accepted.Job.Id);
    }

    /// <summary>Stops the real worker under current permission loss, then proves fresh continuation cannot substitute an existing approved job for current authority.</summary>
    private static async Task DeniedContinuationAsync(ApplicationHttpFixture test, PlatformDbContext database, string id, string version, string jobId)
    {
        Assert.True(await GrantBatchOperation.ExecuteAsync(test.Environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(jobId),
            test.Environment.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken));
        var history = await GrantAuditWitness.ReadAsync(database, Guid.Parse(jobId), TestContext.Current.CancellationToken);
        history.Require();
        Assert.Equal(GrantJobState.Stopped, history.Job.State);
        Assert.Equal(GrantJobFailure.Authority, history.Job.Failure);
        var body = JsonSerializer.Serialize(new GrantRevocation(Reason, true, ApplicationVersion.Create(jobId, history.Job.Revision)), JsonSerializerOptions.Web);
        await DeniedAsync(test, database, id, version, Guid.NewGuid().ToString(GuidFormat), body, jobId);
    }

    /// <summary>Both genuine HTTP surfaces deny exact intent while an ordinary application read proves live authentication and independent read permission.</summary>
    private static async Task DeniedAsync(ApplicationHttpFixture test, PlatformDbContext database, string id, string version, string key, string body = Body, string? jobId = null)
    {
        var before = await SnapshotAsync(database, id);
        foreach (var direct in new[] { false, true })
        {
            using var denied = await test.GrantAsync(id, body, version, key, direct, jobId);
            await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.Forbidden);
            using var control = direct ? await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + id)
                : await test.Browser.GetAsync(ApplicationHttpFixture.Collection + Separator + id, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, control.StatusCode);
        }
        Assert.Equal(before, await SnapshotAsync(database, id));
    }

    /// <summary>With native/source authority still live, the last fresh second admits once; equality denies new/replay admission while protected status remains readable.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_LiveComponentAuthority_When_MfaReachesEquality_Then_AdmissionIsForbiddenButStatusStillReads(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, 1, 1);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var verified = await database.Set<AdministratorSession>().Where(row => row.PublicId == test.Fixture.Identity.Session)
            .Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Fixture.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + LastFreshSecond);
        var intent = test.Intent();
        var accepted = await test.AdmitAsync(intent);
        Assert.Equal(test.Fixture.Clock.Now.AddSeconds(1), accepted.ExpiresAt);
        test.Fixture.Clock.Now = test.Fixture.Clock.Now.AddSeconds(1);
        var before = await SnapshotAsync(database, test.Id);
        foreach (var request in new[] { intent, test.Intent() })
        {
            var denied = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.AdmitAsync(request));
            Assert.Equal(403, denied.Status);
        }
        var page = await GrantReadOperation.ReadAsync(test.Fixture.Api.GetRequiredService<IServiceScopeFactory>(), test.Fixture.Identity, test.Id,
            Guid.Parse(accepted.Id), test.Fixture.Api.GetRequiredService<ApiResourceProfile>(), test.Fixture.Clock, test.Token);
        Assert.Equal(accepted.Id, Assert.Single(page.Items).Id);
        Assert.True(page.ExpiresAt > test.Fixture.Clock.Now);
        Assert.Equal(before, await SnapshotAsync(database, test.Id));
    }
}
