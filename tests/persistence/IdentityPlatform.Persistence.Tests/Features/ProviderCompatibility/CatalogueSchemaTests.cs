using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Uses real provider constraints and native scope ownership to reject invalid catalogue policy and lost-update writes.</summary>
public sealed class CatalogueSchemaTests
{
    private const string Postgres = "PostgreSql";
    private const string Resource = "orders-api";
    private const string CaseResource = "Orders-Api";
    private const string Display = "Orders";
    private const string Owner = "Operations";
    private const string ScopeName = "orders.read";
    private const string ApiKind = "api";
    private const string NoDisclosure = "none";
    private const string EmailDisclosure = "email";
    private const string Consent = "Read orders";
    private const int NextRevision = 2;

    /// <summary>Actual unique/positive-revision constraints enforce ordinal identities and reject malformed rows independently of input validation.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_FreshCatalogue_When_OrdinalDuplicatesAndInvalidRevisionWritten_Then_DatabaseRejectsOnlyInvalidRows(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await AddAudienceAsync(fixture, Resource, 1);
        await AddAudienceAsync(fixture, CaseResource, 1);
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAudienceAsync(fixture, Resource, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAudienceAsync(fixture, ScopeName, 0));
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal([CaseResource, Resource], (await database.Set<ApiAudience>().Select(row => row.Resource).ToListAsync(fixture.Token)).Order(StringComparer.Ordinal));
    }

    /// <summary>Two independent contexts cannot silently overwrite the first committed aggregate revision.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoCurrentAudienceReaders_When_FirstRevisionCommits_Then_SecondStaleWriteFails(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await AddAudienceAsync(fixture, Resource, 1);
        await using var first = fixture.Scope();
        await using var second = fixture.Scope();
        var one = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var two = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var a = await one.Set<ApiAudience>().SingleAsync(fixture.Token);
        var b = await two.Set<ApiAudience>().SingleAsync(fixture.Token);
        a.Revision = NextRevision;
        a.Enabled = false;
        Assert.Equal(1, await one.SaveChangesAsync(fixture.Token));
        b.Revision = NextRevision;
        b.DisplayName = Owner;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => two.SaveChangesAsync(fixture.Token));
        await using var observer = fixture.Scope();
        var actual = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApiAudience>().AsNoTracking().SingleAsync(fixture.Token);
        Assert.False(actual.Enabled);
        Assert.Equal(Display, actual.DisplayName);
    }

    /// <summary>A native manager cannot remove a scope behind product policy, and an API scope cannot acquire human disclosure through direct storage.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedNativeScope_When_ConflictingDisclosureOrNativeDeletionAttempted_Then_ConstraintsPreservePolicy(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var scope = fixture.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = ScopeName, DisplayName = Display }, fixture.Token);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = new ScopePolicy { ScopeId = (await manager.GetIdAsync(native, fixture.Token))!, Kind = ApiKind, Disclosure = NoDisclosure, ConsentText = Consent };
        database.Add(policy);
        await database.SaveChangesAsync(fixture.Token);
        policy.Disclosure = EmailDisclosure;
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
        await using var deletion = fixture.Scope();
        var other = deletion.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var current = await other.FindByIdAsync(policy.ScopeId, fixture.Token);
        Assert.NotNull(current);
        await Assert.ThrowsAsync<DbUpdateException>(() => other.DeleteAsync(current, fixture.Token).AsTask());
        await using var observer = fixture.Scope();
        var actual = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ScopePolicy>().AsNoTracking().SingleAsync(fixture.Token);
        Assert.Equal(NoDisclosure, actual.Disclosure);
    }

    /// <summary>Protects native scope policy from stale independent editors and nonpositive revision writes.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoPolicyReaders_When_FirstRevisionCommits_Then_StaleAndNonpositiveWritesCannotReplaceIt(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var first = fixture.Scope();
        var manager = first.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = ScopeName }, fixture.Token);
        var one = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = new ScopePolicy { ScopeId = (await manager.GetIdAsync(native, fixture.Token))!, Kind = ApiKind, Disclosure = NoDisclosure, ConsentText = Consent };
        one.Add(policy);
        await one.SaveChangesAsync(fixture.Token);
        await using var second = fixture.Scope();
        var two = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stale = await two.Set<ScopePolicy>().SingleAsync(fixture.Token);
        policy.Revision = NextRevision;
        policy.Enabled = false;
        await one.SaveChangesAsync(fixture.Token);
        stale.Revision = NextRevision;
        stale.ConsentText = Owner;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => two.SaveChangesAsync(fixture.Token));
        policy.Revision = 0;
        await Assert.ThrowsAsync<DbUpdateException>(() => one.SaveChangesAsync(fixture.Token));
        await using var observer = fixture.Scope();
        var actual = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ScopePolicy>().SingleAsync(fixture.Token);
        Assert.Equal(NextRevision, actual.Revision);
        Assert.False(actual.Enabled);
        Assert.Equal(NoDisclosure, actual.Disclosure);
        Assert.Equal(Consent, actual.ConsentText);
    }

    /// <summary>Uses fresh contexts so a rejected insert cannot contaminate subsequent constraint observations.</summary>
    private static async Task AddAudienceAsync(ManagedSchemaFixture fixture, string resource, long revision)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(new ApiAudience { Id = Guid.NewGuid().ToString(), Resource = resource, DisplayName = Display, Owner = Owner, Revision = revision });
        await database.SaveChangesAsync(fixture.Token);
    }
}
