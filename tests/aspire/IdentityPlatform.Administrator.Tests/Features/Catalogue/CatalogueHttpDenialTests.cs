using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Requires real native authentication, independent current permission and catalogue-purpose CSRF before any mutation can commit.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueHttpDenialTests(BootstrapFixture fixture)
{
    private const string ApiPath = "/api/v1/audiences";
    private const string BrowserPath = "/bff/api/v1/audiences";
    private const string Create = "{\"resource\":\"orders-api\",\"displayName\":\"Orders\",\"owner\":\"Operations\",\"enabled\":true}";
    private const string Permission = "audiences.write";

    /// <summary>Missing bearer, missing native proof and a genuine application-purpose proof all deny without catalogue or audit changes.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualSignedInBrowser_When_BearerOrCatalogueProofOrPermissionIsMissing_Then_NoCatalogueStateOrAuditChanges(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var before = await CatalogueHttpEvidence.SnapshotAsync(test);
        using var anonymous = await test.CatalogueAsync(HttpMethod.Post, ApiPath, Create, authenticated: false);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var missing = await test.CatalogueAsync(HttpMethod.Post, BrowserPath, Create, direct: false);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var wrongPurpose = await test.CatalogueAsync(HttpMethod.Post, BrowserPath, Create, direct: false, csrf: await test.TokenAsync());
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        Assert.Equal(before, await CatalogueHttpEvidence.SnapshotAsync(test));
        await using var setup = test.Environment.Api!.Services.CreateAsyncScope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        using var forbidden = await test.CatalogueAsync(HttpMethod.Post, BrowserPath, Create, direct: false, csrf: await CatalogueHttpTests.ProofAsync(test));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(before, await CatalogueHttpEvidence.SnapshotAsync(test));
    }
}
