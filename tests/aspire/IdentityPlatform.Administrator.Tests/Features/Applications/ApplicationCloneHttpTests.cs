using System.Globalization;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises clone routing through real HTTPS bearer and browser-cookie/CSRF boundaries with independent persisted provenance.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationCloneHttpTests(BootstrapFixture fixture)
{
    private const string Api = "/api/v1/applications/";
    private const string Browser = "/bff/api/v1/applications/";
    private const string Suffix = "/clone";
    private const string Client = "http-clone";
    private const string Display = "HTTP clone";
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string Wildcard = "*";
    private const string Unknown = "\"secret\":\"forbidden\",";
    private const string Duplicate = "\"confirmed\":false,";
    private const string Empty = "{}";
    private const string InvalidProof = "invalid-native-csrf";
    private const int StaleRevision = 2;

    /// <summary>Strict input and source preconditions precede one disabled creation; duplicate output is denied and subsequent reads reveal no credential.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineAdministrator_When_CloneCrossesHttpsBoundary_Then_PreconditionsAndNewDestinationRemainBound(string provider, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var source = await ApplicationHttpTests.CreatedAsync(create);
        var version = create.Headers.ETag!.ToString();
        var path = Path(source.Application.Id, direct);
        var proof = direct ? null : await test.TokenAsync();
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, source.Application.Id);
        await RejectAsync(test, source.Application.Id, path, version, direct, proof);
        Assert.Equal(before.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test, source.Application.Id)).Digest);
        using var response = await test.CatalogueAsync(HttpMethod.Post, path, Input(test.Now), version, direct: direct, csrf: proof);
        var clone = await ApplicationHttpTests.CreatedAsync(response);
        Assert.NotEqual(source.Application.Id, clone.Application.Id);
        Assert.False(clone.Application.Enabled);
        Assert.Equal(Client, clone.Application.ClientId);
        Assert.Equal(Display, clone.Application.DisplayName);
        Assert.Equal(source.Application.RedirectUris, clone.Application.RedirectUris);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(test.Now.AddHours(1).ToUnixTimeSeconds()), clone.Application.Credential!.ExpiresAt);
        Assert.Equal(ApplicationVersion.Create(clone.Application.Id, 1), response.Headers.ETag!.ToString());
        Assert.True(clone.ClientSecret != source.ClientSecret);
        await RequirePersistedAsync(test, source.Application.Id, version, clone);
        var after = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, source.Application.Id);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Policy), ApplicationUpdateSnapshot.Hash(after.Policy));
        using var duplicate = await test.CatalogueAsync(HttpMethod.Post, path, Input(test.Now), version, direct: direct, csrf: proof);
        await ApplicationHttpTests.ProblemAsync(duplicate, HttpStatusCode.Conflict);
        Assert.Equal(after.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test, source.Application.Id)).Digest);
    }

    /// <summary>Checks genuine missing/invalid proof and exact JSON/precondition failures before any allowed destination exists.</summary>
    private static async Task RejectAsync(ApplicationHttpFixture test, string id, string path, string version, bool direct, string? proof)
    {
        using var missing = await test.CatalogueAsync(HttpMethod.Post, path, Input(test.Now), direct: direct, csrf: proof);
        await ApplicationHttpTests.ProblemAsync(missing, HttpStatusCode.PreconditionRequired);
        foreach (var tag in new[] { Wildcard, ApplicationVersion.Create(id, StaleRevision) })
        {
            using var stale = await test.CatalogueAsync(HttpMethod.Post, path, Input(test.Now), tag, direct: direct, csrf: proof);
            await ApplicationHttpTests.ProblemAsync(stale, HttpStatusCode.PreconditionFailed);
        }
        foreach (var body in new[] { Empty, Input(test.Now).Insert(1, Unknown), Input(test.Now).Insert(1, Duplicate) })
        {
            using var invalid = await test.CatalogueAsync(HttpMethod.Post, path, body, version, direct: direct, csrf: proof);
            await ApplicationHttpTests.ProblemAsync(invalid, HttpStatusCode.BadRequest);
        }
        using var anonymous = await test.CatalogueAsync(HttpMethod.Post, path, Input(test.Now), version, direct: direct, authenticated: false);
        await ApplicationHttpTests.ProblemAsync(anonymous, direct ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest);
        if (!direct)
        {
            using var invalid = await test.CatalogueAsync(HttpMethod.Post, path, Input(test.Now), version, direct: false, csrf: InvalidProof);
            await ApplicationHttpTests.ProblemAsync(invalid, HttpStatusCode.BadRequest);
        }
    }

    /// <summary>Reads immutable source provenance and both later projections independently from the original mutation output.</summary>
    private static async Task RequirePersistedAsync(ApplicationHttpFixture test, string source, string version, ApplicationCredentialOutput clone)
    {
        await using var scope = test.Scope();
        var detail = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationCloneAuditDetail>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(source, detail.SourceApplicationId);
        Assert.Equal(version, detail.SourceVersion);
        foreach (var direct in new[] { false, true })
        {
            using var response = await test.CatalogueAsync(HttpMethod.Get, (direct ? Api : Browser) + Uri.EscapeDataString(clone.Application.Id), direct: direct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(ApplicationVersion.Create(clone.Application.Id, 1), response.Headers.ETag!.ToString());
            Assert.True(response.Headers.CacheControl!.NoStore);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.False(body.Contains(clone.ClientSecret, StringComparison.Ordinal));
            var read = JsonSerializer.Deserialize<ApplicationDetails>(body, JsonSerializerOptions.Web)!;
            Assert.False(read.Enabled);
            Assert.Equal(clone.Application.Id, read.Id);
        }
    }

    /// <summary>Builds an independent confirmed four-field request with a future UTC deadline.</summary>
    internal static string Input(DateTimeOffset now) => JsonSerializer.Serialize(new
    {
        confirmed = true,
        clientId = Client,
        displayName = Display,
        credentialExpiresAt = now.AddHours(1).ToString(Format, CultureInfo.InvariantCulture)
    });

    /// <summary>Uses the documented fixed source route on the selected authenticated surface.</summary>
    internal static string Path(string id, bool direct) => (direct ? Api : Browser) + Uri.EscapeDataString(id) + Suffix;
}
