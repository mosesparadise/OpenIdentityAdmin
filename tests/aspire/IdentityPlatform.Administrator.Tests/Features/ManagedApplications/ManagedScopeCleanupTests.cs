using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises retained-reference release through actual cache cleanup while preserving equal/future retention and original audience identities.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedScopeCleanupTests(BootstrapFixture fixture)
{
    private const string ScopeName = "retained.scope";
    private const string AudienceId = "retained-audience";
    private const string Resource = "retained-api";
    private const string Display = "Retained policy";
    private const int RetentionSeconds = 14 * 24 * 60 * 60;
    private const int LifetimeSeconds = 300;

    /// <summary>Only a fully eligible terminal parent releases its original scope/audience references; moving native resources cannot release them early.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExpiredAndRetainedReferences_When_RealCacheCleansEligibleParents_Then_EqualityAndOriginalAudienceRemainProtected(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var now = test.Http.Now.ToUnixTimeSeconds();
        var expired = Expired(test, now);
        var equality = Expired(test, now);
        equality.RetainUntil = now;
        var future = Expired(test, now);
        future.ObservedAt = now + 1;
        var scopeId = await SeedAsync(test, expired, equality, future);
        using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(response);
        await using var observer = test.Http.Scope();
        var services = observer.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var references = await database.Set<ManagedScopeReference>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, references.Length);
        Assert.DoesNotContain(references, row => row.SelectorDigest == expired.SelectorDigest);
        Assert.Contains(references, row => row.SelectorDigest == equality.SelectorDigest);
        Assert.Contains(references, row => row.SelectorDigest == future.SelectorDigest);
        Assert.False(await database.Set<ManagedLoginInteraction>().AnyAsync(row => row.SelectorDigest == expired.SelectorDigest, TestContext.Current.CancellationToken));
        Assert.True(await CatalogueReferences.ScopeAsync(services, scopeId, ScopeName, TestContext.Current.CancellationToken));
        Assert.True(await CatalogueReferences.AudienceAsync(services, AudienceId, Resource, TestContext.Current.CancellationToken));
        Assert.False(await CatalogueNativeReferences.AudienceAsync(services, Resource, TestContext.Current.CancellationToken));
    }

    /// <summary>Uses real native scope and restrictive audience relationships; the native scope has no resource mapping, so only retained identity can protect the audience.</summary>
    private static async Task<string> SeedAsync(ManagedCacheTestContext test, params ManagedLoginInteraction[] rows)
    {
        await using var observer = test.Http.Scope();
        var services = observer.ServiceProvider;
        var manager = services.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = ScopeName, DisplayName = Display }, TestContext.Current.CancellationToken);
        var id = (await manager.GetIdAsync(native, TestContext.Current.CancellationToken))!;
        var database = services.GetRequiredService<PlatformDbContext>();
        database.Add(new ApiAudience { Id = AudienceId, Resource = Resource, DisplayName = Display, Owner = Display });
        database.AddRange(rows);
        database.AddRange(rows.Select(row => new ManagedScopeReference { SelectorDigest = row.SelectorDigest, ScopeId = id, AudienceId = AudienceId }));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }

    /// <summary>Creates explicitly synthetic cleanup metadata beyond the original minimum retention; no proof is accepted as user consent.</summary>
    private static ManagedLoginInteraction Expired(ManagedCacheTestContext test, long now)
    {
        var row = ManagedCacheCapacityTests.Row(test, true);
        row.CreatedAt = now - RetentionSeconds - LifetimeSeconds;
        row.ExpiresAt = row.CreatedAt + LifetimeSeconds;
        row.ObservedAt = row.ExpiresAt;
        row.RetainUntil = now - 1;
        return row;
    }
}
