using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises actual paired-null and restrictive relationships independently of protocol validation or EF relationship fixup.</summary>
public sealed class ManagedScopeSchemaTests
{
    private const string InteractionTarget = "interaction";
    private const string ScopeTarget = "scope";
    private const string AudienceTarget = "audience";
    private const string PartialPolicy = "partial";
    private const string EmptyJson = "{}";
    private const string ScopeName = "retained.policy.scope";
    private const string OriginalAudience = "original-audience";
    private const string OriginalResource = "original-resource";
    private const string Display = "Original";
    private const string Owner = "Tests";

    /// <summary>Neither native scope, original audience nor owning interaction can disappear while retained references exist.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetainedPolicyReferences_When_ParentsAreDeletedOrPolicyIsPartial_Then_RealConstraintsPreserveBindings(string provider)
    {
        await using var test = await ManagedSchemaFixture.CreateAsync(provider);
        var (selector, scopeId, audienceId) = await SeedAsync(test);
        foreach (var target in new[] { InteractionTarget, ScopeTarget, AudienceTarget, PartialPolicy })
        {
            await using var observer = test.Scope();
            var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
            if (target == PartialPolicy)
            {
                var row = await database.Set<ManagedLoginInteraction>().SingleAsync(test.Token);
                row.PolicySnapshot = EmptyJson;
            }
            else if (target == InteractionTarget)
            {
                database.Remove(await database.Set<ManagedLoginInteraction>().SingleAsync(test.Token));
            }
            else if (target == ScopeTarget)
            {
                database.Remove(await database.Set<OpenIddictEntityFrameworkCoreScope>().SingleAsync(test.Token));
            }
            else
            {
                database.Remove(await database.Set<ApiAudience>().SingleAsync(test.Token));
            }
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(test.Token));
        }
        await using var read = test.Scope();
        var actual = await read.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ManagedScopeReference>().AsNoTracking().SingleAsync(test.Token);
        Assert.Equal(selector, actual.SelectorDigest);
        Assert.Equal(scopeId, actual.ScopeId);
        Assert.Equal(audienceId, actual.AudienceId);
    }

    /// <summary>Creates independent synthetic relational dependencies without pretending that a reference or legacy row is a genuine consent proof.</summary>
    private static async Task<(string Selector, string Scope, string Audience)> SeedAsync(ManagedSchemaFixture test)
    {
        await using var scope = test.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        const int MaximumScopeIdLength = 100;
        var native = new OpenIddictEntityFrameworkCoreScope { Id = new string('s', MaximumScopeIdLength), Name = ScopeName };
        await manager.CreateAsync(native, test.Token);
        var id = (await manager.GetIdAsync(native, test.Token))!;
        var row = ManagedSchemaRows.Reservation();
        var audience = new ApiAudience { Id = OriginalAudience, Resource = OriginalResource, DisplayName = Display, Owner = Owner };
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.AddRange(row, audience, new ManagedScopeReference { SelectorDigest = row.SelectorDigest, ScopeId = id, AudienceId = audience.Id });
        await database.SaveChangesAsync(test.Token);
        return (row.SelectorDigest, id, audience.Id);
    }
}
