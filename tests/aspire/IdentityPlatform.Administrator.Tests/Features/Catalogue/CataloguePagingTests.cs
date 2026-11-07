using System.Globalization;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Admin.Api.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Executes actual provider-translated prefix, ordinal ordering, paging and bounded lookahead rather than only validating response DTOs.</summary>
[Collection("administrator-bootstrap")]
public sealed class CataloguePagingTests(BootstrapFixture fixture)
{
    private const int Count = 26;
    private const int PageSize = 25;
    private const int FirstPage = 1;
    private const int SecondPage = 2;
    private const int Created = 201;
    private const string Prefix = "orders.read";
    private const string Upper = "Orders.read";
    private const string Format = "D2";
    private const string Display = "Read orders";
    private const string Identity = "identity";
    private const string None = "none";
    private const string Consent = "Read information";
    private const string Search = "search";
    private const string Page = "page";
    private const int OverflowLength = 201;

    /// <summary>Real25+1 pagination excludes uppercase prefix aliases and protected system IDs; malformed lookahead cannot masquerade as a valid bounded page.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedScopesAcrossPageBoundary_When_OrdinalPrefixAndLookaheadExecute_Then_ExactPagesExcludeSystemAliasesAndRejectOverflow(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var ids = await SeedAsync(test);
        var first = await ReadAsync(test, Prefix, FirstPage);
        Assert.Equal(PageSize, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(ids.Take(PageSize), first.Items.Select(row => row.Id));
        var second = await ReadAsync(test, Prefix, SecondPage);
        Assert.Equal(ids[^1], Assert.Single(second.Items).Id);
        Assert.False(second.HasMore);
        Assert.Equal(Upper, Assert.Single((await ReadAsync(test, Upper, FirstPage)).Items).Name);
        var all = await ReadAsync(test, string.Empty, FirstPage);
        Assert.Equal(Upper, all.Items[0].Name);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(test.Token);
        Assert.DoesNotContain(all.Items, row => row.Id == profile.ScopeId);
        var lastId = ids[^1];
        Assert.Equal(1, await database.Set<OpenIddictEntityFrameworkCoreScope>().Where(row => row.Id == lastId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.DisplayName, new string('x', OverflowLength)), test.Token));
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var error = await Assert.ThrowsAsync<CatalogueRejected>(() => ReadAsync(test, Prefix, FirstPage));
        Assert.Equal(CatalogueFailure.Unsupported, error.Failure);
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
    }

    /// <summary>Creates valid entries through production and introduces one explicit policy on the protected native scope to challenge the system-ID exclusion.</summary>
    private static async Task<string[]> SeedAsync(ApplicationMutationFixture test)
    {
        List<string> ids = [];
        foreach (var name in Enumerable.Range(0, Count).Select(index => Prefix + index.ToString(Format, CultureInfo.InvariantCulture)).Append(Upper))
        {
            var result = await CatalogueMutationTests.ExecuteAsync(test, CatalogueIntent.Create(new ScopeInput(name, Display, Identity, true, Consent, null, None))!);
            Assert.Equal(Created, result.Status);
            if (name != Upper)
            {
                ids.Add(result.Scope!.Id);
            }
        }
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(test.Token);
        database.Add(new ScopePolicy { ScopeId = profile.ScopeId, Kind = Identity, Disclosure = None, ConsentText = Consent });
        await database.SaveChangesAsync(test.Token);
        return ids.ToArray();
    }

    /// <summary>Uses the genuine read coordinator and independent query input with the original authenticated source and request budget.</summary>
    private static async Task<CataloguePage> ReadAsync(ApplicationMutationFixture test, string prefix, int page)
    {
        var query = ApplicationListQuery.Parse([new(Search, [prefix]), new(Page, [page.ToString(CultureInfo.InvariantCulture)])])!;
        using var budget = new ApplicationReadBudget(test.Clock, test.Token);
        var result = await CatalogueReadOperation.ReadAsync(test.Api.GetRequiredService<IServiceScopeFactory>(), test.Identity, CatalogueResource.Scope, null, null,
            query, test.Api.GetRequiredService<ApiResourceProfile>(), test.Clock, budget.Token);
        return Assert.IsType<CataloguePage>(result.Output);
    }
}
