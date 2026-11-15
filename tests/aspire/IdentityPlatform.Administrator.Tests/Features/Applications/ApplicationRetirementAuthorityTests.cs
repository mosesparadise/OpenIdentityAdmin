using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires current independent lifecycle and grant authority for initial and replay admission under one original fresh-MFA epoch.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementAuthorityTests(BootstrapFixture fixture)
{
    private const int BeyondReplayDays = 2;

    private const string Revoked = "revoked";

    private const string Disable = "applications.disable";
    private const string Revoke = "grants.revoke";
    private const string Write = "applications.write";
    private const int LastFreshSecond = 299;

    /// <summary>Either missing required permission denies initial and exact replay without writes; application-write permission is an independent control.</summary>
    [Theory]
    [InlineData("PostgreSql", Disable, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Revoke, Traits = ["Provider", "PostgreSql", "CriticalDecision", "retirement-dual-permission"])]
    [InlineData("PostgreSql", Write, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Disable, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Revoke, Traits = ["Provider", "SqlServer", "CriticalDecision", "retirement-dual-permission"])]
    [InlineData("SqlServer", Write, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentAuthority_When_OnePermissionIsRemoved_Then_InitialAndReplayRequireExactlyBothRetirementPermissions(string provider, string permission)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var intent = test.Intent();
        var removed = await RemoveAsync(test, permission);
        if (permission == Write)
        {
            var accepted = await test.RetireAsync(intent);
            Assert.Equal(accepted, await test.RetireAsync(intent));
            return;
        }
        var before = await test.SnapshotAsync();
        Assert.Equal(403, (await ApplicationRetirementCausalWitness.AuthorityAsync(test, intent, ApplicationRetirementCausalWitness.Dual)).Status);
        Assert.Equal(before, await test.SnapshotAsync());
        await RestoreAsync(test, removed);
        await test.RetireAsync(intent);
        await RemoveAsync(test, permission);
        before = await test.SnapshotAsync();
        Assert.Equal(403, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent))).Status);
        Assert.Equal(before, await test.SnapshotAsync());
    }

    /// <summary>A saved new grant approval cannot outlive the original MFA receipt or current grant permission before terminal commit.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql", "CriticalDecision", "retirement-current-authority"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer", "CriticalDecision", "retirement-current-authority"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_StagedRetirement_When_PermissionOrOriginalMfaEnds_Then_PolicyApprovalReceiptAndAuditRollBack(string provider, bool expireMfa)
    {
        var change = new GrantAdmissionChange(expireMfa);
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider, change);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var verified = await database.Set<AdministratorSession>().Where(row => row.PublicId == test.Fixture.Identity.Session)
            .Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Fixture.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + LastFreshSecond);
        change.Clock = test.Fixture.Clock;
        var before = await test.SnapshotAsync();
        change.Armed = true;
        var denied = await ApplicationRetirementCausalWitness.AuthorityAsync(test, test.Intent(), ApplicationRetirementCausalWitness.Authority);
        Assert.True(change.Observed);
        Assert.Equal(expireMfa ? 401 : 403, denied.Status);
        Assert.Equal(before, await test.SnapshotAsync());
    }

    /// <summary>At the exact MFA boundary both first and repeated retirement deny, while durable revocation remains independent of session/MFA lifetime.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_LastFreshSecondAdmission_When_MfaExpires_Then_ReplayDeniesButRecordedWorkerCanFinish(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        await test.Grants.SeedAsync(test.Id, 1, 1);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var verified = await database.Set<AdministratorSession>().Where(row => row.PublicId == test.Fixture.Identity.Session)
            .Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Fixture.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + LastFreshSecond);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        Assert.Equal(test.Fixture.Clock.Now.AddSeconds(1), accepted.Job.ExpiresAt);
        test.Fixture.Clock.Now = test.Fixture.Clock.Now.AddSeconds(1);
        var before = await test.SnapshotAsync();
        Assert.Equal(403, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent))).Status);
        Assert.Equal(before, await test.SnapshotAsync());
        Assert.Equal(1, await database.Set<AdministratorSession>().Where(row => row.PublicId == test.Fixture.Identity.Session)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), test.Token));
        test.Fixture.Clock.Now = test.Fixture.Clock.Now.AddDays(BeyondReplayDays);
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.Equal(Revoked, Assert.Single((await test.Grants.StatusesAsync(test.Id)).Tokens));
        Assert.True((await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id)).Policy.Retired);
    }

    /// <summary>Removes actual stored claims without touching native authentication or unrelated permissions.</summary>
    internal static async Task<IdentityRoleClaim<string>[]> RemoveAsync(ApplicationRetirementTestContext test, string permission)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var claims = await database.RoleClaims.AsNoTracking().Where(row => row.ClaimValue == permission).ToArrayAsync(test.Token);
        Assert.NotEmpty(claims);
        Assert.Equal(claims.Length, await database.RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(test.Token));
        return claims;
    }

    /// <summary>Restores the same independently recorded claim values with native generated identities.</summary>
    internal static async Task RestoreAsync(ApplicationRetirementTestContext test, IdentityRoleClaim<string>[] claims)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.RoleClaims.AddRange(claims.Select(row => new IdentityRoleClaim<string>
        {
            RoleId = row.RoleId,
            ClaimType = row.ClaimType,
            ClaimValue = row.ClaimValue
        }));
        await database.SaveChangesAsync(test.Token);
    }
}
