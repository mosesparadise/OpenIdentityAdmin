using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Admin.Api.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Exercises actual catalogue manager/policy/audit writes using current genuine administrator sessions on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueMutationTests(BootstrapFixture fixture)
{
    private const string Resource = "orders-api";
    private const string Display = "Orders";
    private const string Owner = "Operations";
    private const string Name = "orders.read";
    private const string Consent = "Read orders";
    private const string Edited = "Read order history";
    private const string Api = "api";
    private const string None = "none";
    private const string Identity = "identity";
    private const string Email = "email";
    private const string ScopePermission = "scopes.write";
    private const int MfaSeconds = 300;
    private const int Created = 201;
    private static readonly int[] DuplicateStatuses = [201, 409];
    private static readonly int[] StaleStatuses = [200, 412];

    /// <summary>Current scoped authority creates native resources and explicit policy, then immutable identities and stale versions protect edits.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentCatalogueAuthority_When_CreateAndVersionedEditCommit_Then_NativePolicyAndAuditsAgree(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var audience = (await ExecuteAsync(test, CatalogueIntent.Create(new AudienceInput(Resource, Display, Owner, true))!)).Audience!;
        var created = await ExecuteAsync(test, CatalogueIntent.Create(new ScopeInput(Name, Display, Api, true, Consent, audience.Id, None))!);
        Assert.Equal(201, created.Status);
        var original = Assert.IsType<CatalogueScope>(created.Scope);
        var version = original.Details(false).Version;
        var command = CatalogueIntent.Edit(original.Id, version, new ScopeInput(Name, Display, Api, false, Edited, audience.Id, None))!;
        var changed = await ExecuteAsync(test, command);
        Assert.Equal(200, changed.Status);
        Assert.False(changed.Scope!.Enabled);
        Assert.Equal(Edited, changed.Scope.ConsentText);
        Assert.Equal(412, (await ExecuteAsync(test, command)).Status);
        var immutable = CatalogueIntent.Edit(original.Id, changed.Scope.Details(false).Version, new ScopeInput(Email, Display, Identity, true, Consent, null, Email))!;
        Assert.Equal(409, (await ExecuteAsync(test, immutable)).Status);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actual = await database.Set<ScopePolicy>().AsNoTracking().SingleAsync(test.Token);
        Assert.False(actual.Enabled);
        Assert.Equal(Edited, actual.ConsentText);
        Assert.Equal(2, actual.Revision);
        var manager = observer.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.FindByIdAsync(original.Id, test.Token);
        Assert.NotNull(native);
        Assert.Equal(Name, await manager.GetNameAsync(native, test.Token));
        Assert.Equal([Resource], await manager.GetResourcesAsync(native, test.Token));
        Assert.Equal(3, await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetType == CatalogueAssertions.ScopeTarget || row.TargetType == CatalogueAssertions.AudienceTarget, test.Token));
    }

    /// <summary>Independent service roots serialize duplicate creations and stale edits without producing a losing audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentCatalogueEditors_When_DuplicateAndStaleCommandsRace_Then_OnlyOneOfEachCommits(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var peer = test.Peer(provider);
        var command = CatalogueIntent.Create(new AudienceInput(Resource, Display, Owner, true))!;
        var creates = await Task.WhenAll(ExecuteAsync(test, command), ExecuteAsync(test, command, peer));
        Assert.Equal(DuplicateStatuses, creates.Select(value => value.Status).Order());
        var original = Assert.Single(creates, value => value.Status == Created).Audience!;
        var edit = CatalogueIntent.Edit(original.Id, original.Details(false).Version, new AudienceInput(Resource, Edited, Owner, false))!;
        var edits = await Task.WhenAll(ExecuteAsync(test, edit), ExecuteAsync(test, edit, peer));
        Assert.Equal(StaleStatuses, edits.Select(value => value.Status).Order());
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actual = await database.Set<ApiAudience>().AsNoTracking().SingleAsync(test.Token);
        Assert.False(actual.Enabled);
        Assert.Equal(Edited, actual.DisplayName);
        Assert.Equal(2, actual.Revision);
        Assert.Equal(2, await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetType == CatalogueAssertions.AudienceTarget, test.Token));
    }

    /// <summary>Scope permission is independent of audience permission, and the exact original MFA deadline denies all policy mutation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RemovedScopeGrantOrExpiredMfa_When_PolicyMutationRequested_Then_NoUnauthorizedRowsOrAuditsPersist(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == ScopePermission).ExecuteDeleteAsync(test.Token) > 0);
        var denied = await ExecuteAsync(test, CatalogueIntent.Create(new ScopeInput(Email, Display, Identity, true, Consent, null, Email))!);
        Assert.Equal(403, denied.Status);
        var allowed = await ExecuteAsync(test, CatalogueIntent.Create(new AudienceInput(Resource, Display, Owner, true))!);
        Assert.Equal(201, allowed.Status);
        var verified = await database.Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds);
        var current = allowed.Audience!;
        var expired = await ExecuteAsync(test, CatalogueIntent.Edit(current.Id, current.Details(false).Version, new AudienceInput(Resource, Edited, Owner, false))!);
        Assert.Equal(403, expired.Status);
        Assert.Empty(await database.Set<ScopePolicy>().AsNoTracking().ToArrayAsync(test.Token));
        var actual = await database.Set<ApiAudience>().AsNoTracking().SingleAsync(test.Token);
        Assert.True(actual.Enabled);
        Assert.Equal(Display, actual.DisplayName);
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetType == CatalogueAssertions.AudienceTarget, test.Token));
    }

    /// <summary>Uses the actual current-authority coordinator and original publication budget, never a test authorization callback.</summary>
    internal static async Task<CatalogueMutationResult> ExecuteAsync(ApplicationMutationFixture test, CatalogueIntent command, ServiceProvider? node = null)
    {
        var services = node ?? test.Api;
        using var budget = new ApplicationReadBudget(test.Clock, test.Token);
        return await CatalogueMutationOperation.ExecuteAsync(services.GetRequiredService<IServiceScopeFactory>(), test.Identity, command,
            services.GetRequiredService<ApiResourceProfile>(), test.Clock, budget, Guid.NewGuid());
    }
}

/// <summary>Names independent observable audit targets without importing implementation audit constants.</summary>
internal static class CatalogueAssertions
{
    internal const string ScopeTarget = "Scope";
    internal const string AudienceTarget = "Audience";
}
