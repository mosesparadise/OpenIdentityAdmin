using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises exact safe detail projections and authority expiration through actual independent HTTPS application hosts.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationDetailsHttpTests(BootstrapFixture fixture)
{
    private const string LegacyId = "CaseDetails";
    private const string LegacyClient = "legacy-details-client";
    private const string LegacyName = "Legacy details";
    private const string Private = "private-native-canary";
    private const string ProtectedClient = "identity-admin-bff";
    private const string Separator = "/";
    private const string DriftType = "WEB";
    private const string Padding = " ";
    private const string NativeWeb = "web";
    private const string DuplicateRedirects = "[\"https://managed.example.test/callback\",\"https://managed.example.test/callback\"]";
    private const int MaximumNativeJson = 131072;
    private const string ValidRedirects = "[\"https://managed.example.test/callback\"]";
    private const int CreateOffsetMinutes = -20;
    private const int CredentialSeconds = 5;
    private const int AdvanceSeconds = 10;
    private static readonly string[] DetailFields = ["clientId", "credential", "displayName", "editable", "expiresAt", "id", "profile", "redirectUris"];

    /// <summary>Unmanaged and system rows expose only bounded read-only metadata; provider case/padding aliases never retrieve another exact identity.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ProtectedAndUnmanagedRows_When_DetailsAreRequested_Then_OnlyExactSafeReadOnlyMetadataIsPublished(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(new OpenIddictEntityFrameworkCoreApplication
        {
            Id = LegacyId,
            ClientId = LegacyClient,
            DisplayName = LegacyName,
            ClientSecret = Private,
            JsonWebKeySet = Private,
            Properties = Private,
            RedirectUris = Private
        });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var system = await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.ClientId == ProtectedClient)
            .Select(row => row.Id!).SingleAsync(TestContext.Current.CancellationToken);
        foreach (var id in new[] { LegacyId, system })
        {
            using var response = await test.Browser.GetAsync(ApplicationHttpTests.Path(id), TestContext.Current.CancellationToken);
            var details = await ReadAsync(response);
            Assert.Null(response.Headers.ETag);
            Assert.False(details.Editable);
            Assert.Null(details.Profile);
            Assert.Null(details.Credential);
            Assert.Empty(details.RedirectUris);
        }
        foreach (var id in new[] { LegacyId.ToLowerInvariant(), LegacyId + Padding })
        {
            using var response = await test.Browser.GetAsync(ApplicationHttpTests.Path(id), TestContext.Current.CancellationToken);
            await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.NotFound);
        }
        Assert.Equal((0, 0), await ApplicationHttpTests.CountsAsync(test));
    }

    /// <summary>Expired credential metadata remains inspectable, while exact native-profile drift fails closed without losing the read-authority distinction.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedCredentialExpiry_When_ReadThenNativeProfileDrifts_Then_MetadataRemainsReadableButDriftIsRejected(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var response = await test.CreateAsync(ApplicationHttpTests.Input(test.Now.AddMinutes(CreateOffsetMinutes).AddSeconds(CredentialSeconds)));
        var created = await ApplicationHttpTests.CreatedAsync(response);
        test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(AdvanceSeconds);
        using var expired = await test.Browser.GetAsync(ApplicationHttpTests.Path(created.Application.Id), TestContext.Current.CancellationToken);
        var details = await ReadAsync(expired);
        Assert.True(details.Editable);
        Assert.True(details.Credential!.ExpiresAt <= test.Now);
        Assert.Equal(response.Headers.ETag, expired.Headers.ETag);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == created.Application.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ApplicationType, DriftType), TestContext.Current.CancellationToken));
        using var drifted = await test.Browser.GetAsync(ApplicationHttpTests.Path(created.Application.Id), TestContext.Current.CancellationToken);
        await ApplicationHttpTests.ProblemAsync(drifted, HttpStatusCode.ServiceUnavailable);
        await RequireMetadataBoundsAsync(test, database, created.Application.Id);
        foreach (var redirects in new[] { DuplicateRedirects, ValidRedirects + new string(' ', MaximumNativeJson - ValidRedirects.Length + 1) })
        {
            await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == created.Application.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.ApplicationType, NativeWeb).SetProperty(row => row.RedirectUris, redirects), TestContext.Current.CancellationToken);
            using var malformed = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + created.Application.Id);
            await ApplicationHttpTests.ProblemAsync(malformed, HttpStatusCode.ServiceUnavailable);
        }
        Assert.Equal((1, 1), await ApplicationHttpTests.CountsAsync(test));
    }

    /// <summary>A valid exact-limit native JSON array succeeds, so the valid padded overflow negative isolates byte bounds rather than malformed syntax.</summary>
    private static async Task RequireMetadataBoundsAsync(ApplicationHttpFixture test, PlatformDbContext database, string id)
    {
        var bounded = ValidRedirects + new string(' ', MaximumNativeJson - ValidRedirects.Length);
        await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ApplicationType, NativeWeb).SetProperty(row => row.RedirectUris, bounded), TestContext.Current.CancellationToken);
        using var response = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + id);
        var details = await ReadAsync(response);
        Assert.True(details.Editable);
        Assert.Single(details.RedirectUris);
    }

    /// <summary>A real issued token cannot read details after native revocation, and an absent bearer never authenticates through cookies.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualApiBearer_When_ItIsMissingOrRevoked_Then_DetailsReturnAuthenticationFailure(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var response = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(response);
        using var missing = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + created.Application.Id, authenticated: false);
        await ApplicationHttpTests.ProblemAsync(missing, HttpStatusCode.Unauthorized);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await ApplicationRevocationWriter.RevokeAsync(database, test.Environment.Issuer.Account.ActorId, test.Environment.Issuer.Account.Grant.SessionId, ApplicationRevocation.Token);
        using var revoked = await test.Browser.GetAsync(ApplicationHttpTests.Path(created.Application.Id), TestContext.Current.CancellationToken);
        await ApplicationHttpTests.ProblemAsync(revoked, HttpStatusCode.Unauthorized);
        using var deniedCreate = await test.CreateAsync(ApplicationHttpTests.Input(test.Now), direct: true);
        await ApplicationHttpTests.ProblemAsync(deniedCreate, HttpStatusCode.Unauthorized);
        using var directRead = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + created.Application.Id);
        await ApplicationHttpTests.ProblemAsync(directRead, HttpStatusCode.Unauthorized);
        Assert.Equal((1, 1), await ApplicationHttpTests.CountsAsync(test));
    }

    /// <summary>Independently enumerates every ordinary detail field and prohibits a private native sentinel without logging response bodies.</summary>
    private static async Task<ApplicationDetails> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(body.Contains(Private, StringComparison.Ordinal));
        using var json = JsonDocument.Parse(body);
        Assert.Equal(DetailFields, json.RootElement.EnumerateObject().Select(value => value.Name).Order().ToArray());
        return JsonSerializer.Deserialize<ApplicationDetails>(body, JsonSerializerOptions.Web)!;
    }
}
