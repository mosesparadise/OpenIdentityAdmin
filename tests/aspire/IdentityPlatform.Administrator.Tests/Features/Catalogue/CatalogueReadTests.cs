using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Admin.Api.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Requires current finite read permissions and exact bounded metadata under actual rollback-only provider ownership.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueReadTests(BootstrapFixture fixture)
{
    private const int IdleSeconds = 1800;
    private const int MfaSeconds = 300;
    private const string ScopePermission = "scopes.read";
    private const string MalformedObject = "{}\u200B";

    /// <summary>Read permission remains usable without fresh MFA, but reads cannot renew authority, mutate native state or inherit another catalogue's grant.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentReadAuthority_When_MfaExpiresOrScopeReadGrantIsRemoved_Then_ReadsStayRollbackOnlyAndPermissionSpecific(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var scope = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var audience = await CatalogueDeletionTest.CreateAsync(test);
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var authority = await AuthorityAsync(test);
        test.Clock.Now = test.Clock.Now.AddSeconds(MfaSeconds);
        var read = await ReadAsync(test, CatalogueResource.Scope, scope.Id);
        var details = Assert.IsType<ScopeDetails>(read.Output);
        Assert.Equal(scope.Version, details.Version);
        Assert.False(details.Referenced);
        Assert.True(details.ExpiresAt > test.Clock.Now);
        Assert.Equal(authority.Expiry, details.ExpiresAt);
        test.Clock.Now = test.Clock.Now.AddSeconds(1);
        var page = Assert.IsType<CataloguePage>((await ReadAsync(test, CatalogueResource.Scope)).Output);
        Assert.Equal(scope.Id, Assert.Single(page.Items).Id);
        Assert.False(page.HasMore);
        Assert.Equal(authority.Expiry, page.ExpiresAt);
        Assert.Equal(authority.Hash, (await AuthorityAsync(test)).Hash);
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
        Assert.Equal(403, (await CatalogueDeletionTest.AdmitAsync(test, scope)).Status);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == ScopePermission).ExecuteDeleteAsync(test.Token) > 0);
        var failure = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => ReadAsync(test, CatalogueResource.Scope, scope.Id));
        Assert.Equal(403, failure.Status);
        Assert.Equal(audience.Id, Assert.IsType<AudienceDetails>((await ReadAsync(test, CatalogueResource.Audience, audience.Id)).Output).Id);
    }

    /// <summary>Malformed metadata that a linguistic database collation may treat as an empty object cannot enter a valid scope projection.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeJsonWithIgnoredCollationCharacter_When_ScopeDetailsRead_Then_ExactProjectionRejectsItWithoutWrites(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        Assert.IsType<ScopeDetails>((await ReadAsync(test, CatalogueResource.Scope, intent.Id)).Output);
        await using var setup = test.Scope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<OpenIddictEntityFrameworkCoreScope>().Where(row => row.Id == intent.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Properties, MalformedObject), test.Token));
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var failure = await Assert.ThrowsAsync<CatalogueRejected>(() => ReadAsync(test, CatalogueResource.Scope, intent.Id));
        Assert.Equal(CatalogueFailure.Unsupported, failure.Failure);
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
    }

    /// <summary>Job status is permission-bound and remains readable after native target deletion; the wrong catalogue cannot reveal it.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CompletedDeletion_When_CurrentReaderChecksJob_Then_StatusSurvivesTargetAndRejectsWrongKind(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, accepted.Id));
        var job = Assert.IsType<CatalogueDeletionDetails>((await ReadAsync(test, CatalogueResource.Audience, job: accepted.Id)).Output);
        Assert.Equal("succeeded", job.State);
        Assert.Equal(intent.Id, job.TargetId);
        var failure = await Assert.ThrowsAsync<CatalogueRejected>(() => ReadAsync(test, CatalogueResource.Scope, job: accepted.Id));
        Assert.Equal(CatalogueFailure.Missing, failure.Failure);
    }

    /// <summary>Independently freezes the signed/native/source minimum and complete persisted authentication/session rows before reads.</summary>
    private static async Task<(DateTimeOffset Expiry, string Hash)> AuthorityAsync(ApplicationMutationFixture test)
    {
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(row => row.PublicId == test.Identity.Session, test.Token);
        var native = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().SingleAsync(row => row.Id == test.Identity.TokenId, test.Token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(row => row.UserId == test.Identity.Subject, test.Token);
        var expiry = new[] { test.Identity.ExpiresAt, new DateTimeOffset(native.ExpirationDate!.Value, TimeSpan.Zero),
            DateTimeOffset.FromUnixTimeSeconds(session.ExpiresAt), DateTimeOffset.FromUnixTimeSeconds(session.LastActivityAt + IdleSeconds) }.Min();
        return (expiry, CatalogueMutationEvidence.Hash(new
        {
            session,
            authentication
        }));
    }

    /// <summary>Uses the production rollback-only operation with an original request cancellation budget and genuine current session identity.</summary>
    internal static async Task<CatalogueReadResult> ReadAsync(ApplicationMutationFixture test, CatalogueResource kind, string? id = null, Guid? job = null)
    {
        using var budget = new ApplicationReadBudget(test.Clock, test.Token);
        return await CatalogueReadOperation.ReadAsync(test.Api.GetRequiredService<IServiceScopeFactory>(), test.Identity, kind, id, job,
            id is null && job is null ? ApplicationListQuery.Parse([]) : null, test.Api.GetRequiredService<ApiResourceProfile>(), test.Clock, budget.Token);
    }
}
