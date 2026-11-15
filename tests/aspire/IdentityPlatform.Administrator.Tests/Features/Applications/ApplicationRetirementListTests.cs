using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves retirement filtering precedes page offset/lookahead and separate navigation preserves historical opaque native identities.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementListTests(BootstrapFixture fixture)
{
    private const string Prefix = "retirement-page-";
    private const string LegacyId = "retired";
    private const string LegacyClient = Prefix + "00";
    private const string RetiredFirst = Prefix + "01";
    private const string ActiveClient = Prefix + "03";
    private const string RetiredLast = Prefix + "05";
    private const string ProtectedClient = "identity-admin-bff";
    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string RetireSuffix = "/retirement";
    private const string RetiredPath = "/retired-applications";
    private const string ApiRoot = "/api/v1";
    private const string BrowserRoot = "/bff/api/v1";
    private const string Separator = "/";
    private const string GuidFormat = "D";
    private const string Query = "?search=" + Prefix + "&pageSize=1&page=";
    private const string Public = "public";
    private const int SecondPage = 2;
    private const int ThirdPage = 3;

    /// <summary>Real API and BFF reads expose exact active/retired partitions, truthful lookahead and an unshadowed native detail named retired.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_InterleavedNativeAndManagedRows_When_RetirementsLeaveNormalList_Then_EachBoundedCollectionPagesBeforeLookaheadAndLegacyDetailsRemainReachable(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        await SeedAsync(test);
        foreach (var direct in new[] { true, false })
        {
            var collection = direct ? ApplicationHttpFixture.ApiCollection : ApplicationHttpFixture.Collection;
            var retired = (direct ? ApiRoot : BrowserRoot) + RetiredPath;
            await PageAsync(test, collection, direct, 1, LegacyClient, true);
            await PageAsync(test, collection, direct, SecondPage, ActiveClient, false);
            await PageAsync(test, collection, direct, ThirdPage, null, false);
            await PageAsync(test, retired, direct, 1, RetiredFirst, true);
            await PageAsync(test, retired, direct, SecondPage, RetiredLast, false);
            await PageAsync(test, retired, direct, ThirdPage, null, false);
            using var legacy = await test.CatalogueAsync(HttpMethod.Get, collection + Separator + LegacyId, direct: direct);
            Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
            var details = (await legacy.Content.ReadFromJsonAsync<ApplicationDetails>(TestContext.Current.CancellationToken))!;
            Assert.Equal(LegacyId, details.Id);
            Assert.False(details.Retired);
            Assert.False(details.Editable);
            Assert.Null(details.Retirement);
            Assert.Null(legacy.Headers.ETag);
            using var normal = await test.CatalogueAsync(HttpMethod.Get, collection, direct: direct);
            Assert.Equal(HttpStatusCode.OK, normal.StatusCode);
            var page = (await normal.Content.ReadFromJsonAsync<ApplicationPage>(TestContext.Current.CancellationToken))!;
            Assert.Contains(page.Items, row => row.ClientId == ProtectedClient);
        }
    }

    /// <summary>Seeds a deliberately unmanaged historic native ID and uses real mutation endpoints for all managed lifecycle changes.</summary>
    private static async Task SeedAsync(ApplicationHttpFixture test)
    {
        await using (var scope = test.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = LegacyId, ClientId = LegacyClient, ClientType = Public });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        foreach (var client in new[] { RetiredFirst, ActiveClient, RetiredLast })
        {
            using var created = await test.CreateAsync(ApplicationHttpTests.Input(test.Now, client));
            var application = await ApplicationHttpTests.CreatedAsync(created);
            if (client == ActiveClient)
            {
                continue;
            }
            using var accepted = await test.CatalogueAsync(HttpMethod.Post, ApplicationHttpFixture.ApiCollection + Separator + application.Application.Id + RetireSuffix,
                Body, created.Headers.ETag!.Tag, direct: true, key: Guid.NewGuid().ToString(GuidFormat));
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
    }

    /// <summary>Asserts independent expected identities and exact pagination shape rather than deriving expectations from the query under test.</summary>
    private static async Task PageAsync(ApplicationHttpFixture test, string path, bool direct, int number, string? expected, bool more)
    {
        using var response = await test.CatalogueAsync(HttpMethod.Get, path + Query + number.ToString(System.Globalization.CultureInfo.InvariantCulture), direct: direct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<ApplicationPage>(TestContext.Current.CancellationToken))!;
        Assert.Equal(number, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(more, page.HasNextPage);
        if (expected is null)
        {
            Assert.Empty(page.Items);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(page.Items).ClientId);
        }
    }
}
