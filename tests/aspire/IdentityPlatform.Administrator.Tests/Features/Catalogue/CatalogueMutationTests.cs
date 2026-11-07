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
    private const string ScopeCreated = "ScopeCreated";
    private const string ScopeEdited = "ScopeEdited";
    private const string AudienceCreated = "AudienceCreated";
    private const string AudienceEdited = "AudienceEdited";
    private const int Updated = 200;
    private const int Created = 201;
    private const int Conflict = 409;
    private const int Stale = 412;
    private const int Forbidden = 403;
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
        var correlation = Guid.NewGuid();
        var at = test.Clock.Now.ToUnixTimeSeconds();
        var created = await ExecuteAsync(test, CatalogueIntent.Create(new ScopeInput(Name, Display, Api, true, Consent, audience.Id, None))!, correlation: correlation);
        Assert.Equal(201, created.Status);
        var original = Assert.IsType<CatalogueScope>(created.Scope);
        await CatalogueMutationEvidence.RequireAuditAsync(test, correlation, ScopeCreated, CatalogueAssertions.ScopeTarget, original.Id, at);
        var version = original.Version;
        var command = CatalogueIntent.Edit(original.Id, version, new ScopeInput(Name, Display, Api, false, Edited, audience.Id, None))!;
        var editCorrelation = Guid.NewGuid();
        var changed = await ExecuteAsync(test, command, correlation: editCorrelation);
        await CatalogueMutationEvidence.RequireAuditAsync(test, editCorrelation, ScopeEdited, CatalogueAssertions.ScopeTarget, original.Id, at);
        Assert.Equal(200, changed.Status);
        Assert.False(changed.Scope!.Enabled);
        Assert.Equal(Edited, changed.Scope.ConsentText);
        await CatalogueMutationEvidence.RequireDeniedAsync(test, command, Stale);
        var immutable = CatalogueIntent.Edit(original.Id, changed.Scope.Version, new ScopeInput(Email, Display, Identity, true, Consent, null, Email))!;
        await CatalogueMutationEvidence.RequireDeniedAsync(test, immutable, Conflict);
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
        var firstCorrelation = Guid.NewGuid();
        var secondCorrelation = Guid.NewGuid();
        var at = test.Clock.Now.ToUnixTimeSeconds();
        var creates = await Task.WhenAll(ExecuteAsync(test, command, correlation: firstCorrelation), ExecuteAsync(test, command, peer, secondCorrelation));
        Assert.Equal(DuplicateStatuses, creates.Select(value => value.Status).Order());
        var original = Assert.Single(creates, value => value.Status == Created).Audience!;
        await CatalogueMutationEvidence.RequireAuditAsync(test, creates[0].Status == Created ? firstCorrelation : secondCorrelation, AudienceCreated, CatalogueAssertions.AudienceTarget, original.Id, at);
        var edit = CatalogueIntent.Edit(original.Id, original.Version, new AudienceInput(Resource, Edited, Owner, false))!;
        firstCorrelation = Guid.NewGuid();
        secondCorrelation = Guid.NewGuid();
        var edits = await Task.WhenAll(ExecuteAsync(test, edit, correlation: firstCorrelation), ExecuteAsync(test, edit, peer, secondCorrelation));
        Assert.Equal(StaleStatuses, edits.Select(value => value.Status).Order());
        await CatalogueMutationEvidence.RequireAuditAsync(test, edits[0].Status == Updated ? firstCorrelation : secondCorrelation, AudienceEdited, CatalogueAssertions.AudienceTarget, original.Id, at);
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
        await CatalogueMutationEvidence.RequireDeniedAsync(test, CatalogueIntent.Create(new ScopeInput(Email, Display, Identity, true, Consent, null, Email))!, Forbidden);
        var allowed = await ExecuteAsync(test, CatalogueIntent.Create(new AudienceInput(Resource, Display, Owner, true))!);
        Assert.Equal(201, allowed.Status);
        var verified = await database.Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds);
        var current = allowed.Audience!;
        await CatalogueMutationEvidence.RequireDeniedAsync(test, CatalogueIntent.Edit(current.Id, current.Version, new AudienceInput(Resource, Edited, Owner, false))!, Forbidden);
        Assert.Empty(await database.Set<ScopePolicy>().AsNoTracking().ToArrayAsync(test.Token));
        var actual = await database.Set<ApiAudience>().AsNoTracking().SingleAsync(test.Token);
        Assert.True(actual.Enabled);
        Assert.Equal(Display, actual.DisplayName);
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetType == CatalogueAssertions.AudienceTarget, test.Token));
    }

    /// <summary>Uses the actual current-authority coordinator and original publication budget, never a test authorization callback.</summary>
    internal static async Task<CatalogueMutationResult> ExecuteAsync(ApplicationMutationFixture test, CatalogueIntent command, ServiceProvider? node = null, Guid? correlation = null)
    {
        var services = node ?? test.Api;
        using var budget = new ApplicationReadBudget(test.Clock, test.Token);
        return await CatalogueMutationOperation.ExecuteAsync(services.GetRequiredService<IServiceScopeFactory>(), test.Identity, command,
            services.GetRequiredService<ApiResourceProfile>(), test.Clock, budget, correlation ?? Guid.NewGuid());
    }
}

/// <summary>Names independent observable audit targets without importing implementation audit constants.</summary>
internal static class CatalogueAssertions
{
    internal const string ScopeTarget = "Scope";
    internal const string AudienceTarget = "Audience";
}
