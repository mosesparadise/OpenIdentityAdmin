using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Checks exact public HTTP field sets and complete persisted catalogue state without trusting permissive DTO deserialization.</summary>
internal static class CatalogueHttpEvidence
{
    private static readonly string[] AudienceFields = ["id", "resource", "displayName", "owner", "enabled", "version", "referenced", "expiresAt"];
    private static readonly string[] JobFields = ["id", "kind", "targetId", "state", "failure", "createdAt", "observedAt", "expiresAt"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.Strict
    };
    private const string AccessType = "urn:ietf:params:oauth:token-type:access_token";
    private const int IdleSeconds = 1800;
    private const int FreshSeconds = 300;
    private const string JsonType = "application/json";
    private const string Cookies = "Set-Cookie";
    private const string NoSniff = "X-Content-Type-Options";
    private const string NoSniffValue = "nosniff";
    private const string StatusField = "status";
    private const string TitleField = "title";
    private const string SignIn = "Sign in again to manage the catalogue.";
    private const string Unavailable = "The catalogue result is unavailable. Check its status before submitting again.";
    private static readonly string[] ProblemFields = ["type", "title", StatusField, "correlationId", "traceId"];

    /// <summary>Requires the exact catalogue recovery title and safe problem schema; application-management titles are a separate contract.</summary>
    internal static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.Location);
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal((int)status, json.RootElement.GetProperty(StatusField).GetInt32());
        Assert.All(json.RootElement.EnumerateObject(), field => Assert.Contains(field.Name, ProblemFields));
        Assert.Equal(status == HttpStatusCode.Unauthorized ? SignIn : Unavailable, json.RootElement.GetProperty(TitleField).GetString());
    }

    /// <summary>Reads only the independently expected safe audience schema and validates native no-store/publication headers.</summary>
    internal static async Task<AudienceDetails> AudienceAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<AudienceDetails>(await BytesAsync(response, AudienceFields), Json)!;

    /// <summary>Reads only the safe durable status schema; private approval, actor and audit properties cannot escape unnoticed.</summary>
    internal static async Task<CatalogueDeletionDetails> JobAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<CatalogueDeletionDetails>(await BytesAsync(response, JobFields), Json)!;

    /// <summary>Checks actual complete JSON shape and headers independently of generated types or serializer tolerances.</summary>
    private static async Task<byte[]> BytesAsync(HttpResponseMessage response, string[] fields)
    {
        Assert.Equal(JsonType, response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.False(response.Headers.Contains(Cookies));
        Assert.Equal(NoSniffValue, Assert.Single(response.Headers.GetValues(NoSniff)));
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(bytes.Length, response.Content.Headers.ContentLength);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(fields.Order(), document.RootElement.EnumerateObject().Select(value => value.Name).Order());
        return bytes;
    }

    /// <summary>Uses original native issuer expiry and source MFA for direct API evidence; the browser also applies its own earlier transport ceiling.</summary>
    internal static async Task RequireExpiryAsync(ApplicationHttpFixture test, DateTimeOffset expiry, bool browser)
    {
        await using var scope = test.Environment.Api!.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var native = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().SingleAsync(row => row.Type == AccessType, token);
        var source = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(row => row.PublicId == test.SourceSession, token);
        var nativeCeiling = DateTimeOffset.FromUnixTimeSeconds(new DateTimeOffset(native.ExpirationDate!.Value, TimeSpan.Zero).ToUnixTimeSeconds());
        var ceiling = new[] { nativeCeiling, DateTimeOffset.FromUnixTimeSeconds(source.ExpiresAt), DateTimeOffset.FromUnixTimeSeconds(source.LastActivityAt + IdleSeconds), DateTimeOffset.FromUnixTimeSeconds(source.FactorVerifiedAt + FreshSeconds) }.Min();
        Assert.True(expiry > test.Now && expiry <= ceiling);
        if (browser)
        {
            Assert.True(expiry <= test.AccessExpiresAt);
        }
    }

    /// <summary>Includes exact native scopes, all product rows and complete audit history around real HTTP denials.</summary>
    internal static async Task<string[]> SnapshotAsync(ApplicationHttpFixture test)
    {
        await using var scope = test.Environment.Api!.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return
        [
            CatalogueMutationEvidence.Hash(await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)),
            CatalogueMutationEvidence.Hash(await database.Set<ScopePolicy>().AsNoTracking().OrderBy(row => row.ScopeId).ToArrayAsync(token)),
            CatalogueMutationEvidence.Hash(await database.Set<ApiAudience>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)),
            CatalogueMutationEvidence.Hash(await database.Set<CatalogueDeletion>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)),
            CatalogueMutationEvidence.Hash(await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token))
        ];
    }
}
