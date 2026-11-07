using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Exercises genuine password/MFA, native bearer HTTP and browser cookie/CSRF through independently hosted API and BFF.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueHttpTests(BootstrapFixture fixture)
{
    private const string Queued = "queued";
    private const string Succeeded = "succeeded";
    private const string AudienceKind = "audience";
    private const string ApiAudiences = "/api/v1/audiences";
    private const string BrowserAudiences = "/bff/api/v1/audiences";
    private const string JobSuffix = "/deletions/";
    private const string TokenSuffix = "/antiforgery";
    private const string Separator = "/";
    private const string Create = "{\"resource\":\"orders-api\",\"displayName\":\"Orders\",\"owner\":\"Operations\",\"enabled\":true}";
    private const string Edit = "{\"resource\":\"orders-api\",\"displayName\":\"Order history\",\"owner\":\"Operations\",\"enabled\":false}";
    private const string Invalid = "{\"resource\":\"orders-api\",\"displayName\":\"Orders\",\"owner\":\"Operations\",\"enabled\":true,\"enabled\":false}";
    private const string GuidFormat = "D";
    private const string RequestToken = "requestToken";

    /// <summary>Actual HTTP CRUD and durable deletion preserve exact strong versions, deny missing/stale preconditions and expose retained status after deletion.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealAdministratorSignIn_When_CatalogueHttpJourneyRuns_Then_VersionsPermissionsAndDurableStatusRemainAuthoritative(string provider, bool browser)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var path = browser ? BrowserAudiences : ApiAudiences;
        var proof = browser ? await ProofAsync(test) : null;
        using var created = await test.CatalogueAsync(HttpMethod.Post, path, Create, direct: !browser, csrf: proof);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var audience = (await CatalogueHttpEvidence.AudienceAsync(created))!;
        Assert.Equal(audience.Version, created.Headers.ETag!.ToString());
        Assert.True(created.Headers.CacheControl!.NoStore);
        await CatalogueHttpEvidence.RequireExpiryAsync(test, audience.ExpiresAt, browser);
        var detail = path + Separator + audience.Id;
        using var missing = await test.CatalogueAsync(HttpMethod.Put, detail, Edit, direct: !browser, csrf: proof);
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        using var invalid = await test.CatalogueAsync(HttpMethod.Put, detail, Invalid, audience.Version, direct: !browser, csrf: proof);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var edited = await test.CatalogueAsync(HttpMethod.Put, detail, Edit, audience.Version, direct: !browser, csrf: proof);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var current = (await CatalogueHttpEvidence.AudienceAsync(edited))!;
        Assert.False(current.Enabled);
        Assert.Equal(current.Version, edited.Headers.ETag!.ToString());
        Assert.Equal("Order history", current.DisplayName);
        Assert.NotEqual(audience.Version, current.Version);
        using var stale = await test.CatalogueAsync(HttpMethod.Put, detail, Edit, audience.Version, direct: !browser, csrf: proof);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await DeleteAsync(test, path, current, browser, proof);
    }

    /// <summary>Requires independently bound admission, actual native completion and retained HTTP status after the target disappears.</summary>
    private static async Task DeleteAsync(ApplicationHttpFixture test, string path, AudienceDetails current, bool browser, string? proof)
    {
        var detail = path + Separator + current.Id;
        var confirm = JsonSerializer.Serialize(new CatalogueDeleteInput(true, Guid.NewGuid().ToString(GuidFormat)), JsonSerializerOptions.Web);
        using var accepted = await test.CatalogueAsync(HttpMethod.Delete, detail, confirm, current.Version, direct: !browser, csrf: proof);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var job = (await CatalogueHttpEvidence.JobAsync(accepted))!;
        Assert.Equal(Queued, job.State);
        Assert.Equal(current.Id, job.TargetId);
        Assert.Equal(AudienceKind, job.Kind);
        Assert.Equal(path + JobSuffix + job.Id.ToString(GuidFormat), accepted.Headers.Location!.OriginalString);
        Assert.True(await CatalogueDeletionProcessor.ExecuteAsync(test.Environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), job.Id,
            test.Environment.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken));
        using var status = await test.CatalogueAsync(HttpMethod.Get, accepted.Headers.Location!.OriginalString, direct: !browser);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(Succeeded, (await CatalogueHttpEvidence.JobAsync(status))!.State);
        using var gone = await test.CatalogueAsync(HttpMethod.Get, detail, direct: !browser);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    /// <summary>Uses the actual catalogue-purpose native proof route; no hand-constructed antiforgery bypass enters a positive journey.</summary>
    internal static async Task<string> ProofAsync(ApplicationHttpFixture test)
    {
        using var response = await test.Browser.GetAsync(BrowserAudiences + TokenSuffix, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty(RequestToken).GetString()!;
    }
}
