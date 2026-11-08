using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises scope assignment through actual HTTPS bearer and cookie/CSRF routes with independent native persistence observations.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopeHttpTests(BootstrapFixture fixture)
{
    internal const string Assign = "{\"scopes\":[\"email\"]}";
    internal const string Clear = "{\"scopes\":[]}";
    internal const string Scope = "{\"name\":\"email\",\"displayName\":\"Email\",\"kind\":\"identity\",\"enabled\":true,\"consentText\":\"Share your email\",\"audienceId\":null,\"disclosure\":\"email\"}";
    private const string ScopeCollection = "/api/v1/scopes";
    private const string Assigned = "ApplicationScopesAssigned";
    private const string Permission = "scp:email";
    private static readonly string[] Fields = ["applicationId", "expiresAt", "scopes"];
    private static readonly string[] Invalid = ["{}", "{\"scopes\":[\"openid\"]}", "{\"scopes\":[\"email\",\"email\"]}", "{\"scopes\":[],\"scopes\":[\"email\"]}"];

    private const string Email = "email";
    private const int AssignedRevision = 2;
    private const int ClearedRevision = 3;
    private const int MaximumMinutes = 5;
    private const int FixedPermissions = 4;
    private const string ApiPrefix = "/api/v1/applications/";
    private const string BrowserPrefix = "/bff/api/v1/applications/";
    private const string Suffix = "/scopes";

    /// <summary>Real authenticated reads and writes preserve unrelated fields, reject malformed/stale input and advance exactly one shared application version and audit.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineAdministrator_When_HttpsAssignmentsAreChanged_Then_NativePermissionsVersionsAndAuditMatchExactly(string provider, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var scope = await test.CatalogueAsync(HttpMethod.Post, ScopeCollection, Scope);
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var application = (await ApplicationHttpTests.CreatedAsync(create)).Application;
        var path = Path(application.Id, direct);
        var proof = direct ? null : await test.TokenAsync();
        var version = create.Headers.ETag!.ToString();
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, application.Id);
        await RejectAsync(test, path, version, direct, proof, before);
        using var read = await test.CatalogueAsync(HttpMethod.Get, path, direct: direct);
        await RequireAsync(test, read, application.Id, version, []);
        using var assigned = await test.CatalogueAsync(HttpMethod.Put, path, Assign, version, direct: direct, csrf: proof);
        await RequireAsync(test, assigned, application.Id, ApplicationVersion.Create(application.Id, AssignedRevision), [Email]);
        var after = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, application.Id);
        Assert.Equal(before.SecretDigest, after.SecretDigest);
        Assert.Equal(before.Redirects, after.Redirects);
        Assert.Equal(before.Display, after.Display);
        Assert.Equal(Assigned, Assert.Single(after.Audits, row => !before.Audits.Any(old => old.Id == row.Id)).Event);
        using var stale = await test.CatalogueAsync(HttpMethod.Put, path, Clear, version, direct: direct, csrf: proof);
        await ApplicationHttpTests.ProblemAsync(stale, HttpStatusCode.PreconditionFailed);
        Assert.Equal(after.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test, application.Id)).Digest);
        using var cleared = await test.CatalogueAsync(HttpMethod.Put, path, Clear, assigned.Headers.ETag!.ToString(), direct: direct, csrf: proof);
        await RequireAsync(test, cleared, application.Id, ApplicationVersion.Create(application.Id, ClearedRevision), []);
    }

    /// <summary>Missing authority, proof, precondition and malformed body must preserve every application field before the positive control.</summary>
    private static async Task RejectAsync(ApplicationHttpFixture test, string path, string version, bool direct, string? proof, ApplicationUpdateSnapshot before)
    {
        using var missing = await test.CatalogueAsync(HttpMethod.Put, path, Assign, direct: direct, csrf: proof);
        await ApplicationHttpTests.ProblemAsync(missing, HttpStatusCode.PreconditionRequired);
        using var denied = await test.CatalogueAsync(HttpMethod.Put, path, Assign, version, direct: direct, authenticated: false);
        await ApplicationHttpTests.ProblemAsync(denied, direct ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest);
        foreach (var body in Invalid)
        {
            using var invalid = await test.CatalogueAsync(HttpMethod.Put, path, body, version, direct: direct, csrf: proof);
            await ApplicationHttpTests.ProblemAsync(invalid, HttpStatusCode.BadRequest);
        }
        Assert.Equal(before.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test, before.Policy.ApplicationId)).Digest);
    }

    /// <summary>Checks the complete finite wire shape and a fresh native row independently of the transport's own parser.</summary>
    private static async Task RequireAsync(ApplicationHttpFixture test, HttpResponseMessage response, string id, string version, string[] expected)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(version, response.Headers.ETag!.ToString());
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Fields, body.RootElement.EnumerateObject().Select(value => value.Name).Order());
        var output = body.RootElement.Deserialize<ApplicationScopes>(JsonSerializerOptions.Web)!;
        Assert.Equal(id, output.ApplicationId);
        Assert.Equal(expected, output.Scopes);
        Assert.True(output.ExpiresAt > test.Now && output.ExpiresAt <= test.Now.AddMinutes(MaximumMinutes));
        await using var scope = test.Scope();
        var native = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreApplication>()
            .AsNoTracking().SingleAsync(row => row.Id == id, TestContext.Current.CancellationToken);
        var permissions = JsonSerializer.Deserialize<string[]>(native.Permissions!)!;
        Assert.Equal(expected.Length != 0, permissions.Contains(Permission, StringComparer.Ordinal));
        Assert.Equal(FixedPermissions + expected.Length, permissions.Length);
    }

    /// <summary>Builds the independently expected versioned route without consulting production routing helpers.</summary>
    internal static string Path(string id, bool direct = false) => (direct ? ApiPrefix : BrowserPrefix) + Uri.EscapeDataString(id) + Suffix;
}
