using System.Globalization;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Verifies actual protected BFF-to-API creates and details through real TLS, native issuer authentication and both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationHttpTests(BootstrapFixture fixture)
{
    private const string Client = "managed-http-client";
    private const string Display = "Managed HTTP client";
    private const string Redirect = "https://managed.example.test/callback";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string Separator = "/";
    private const string AuditTarget = "Application";
    private const string WritePermission = "applications.write";
    private const string ReadPermission = "applications.read";
    private const string Missing = "missing-native-application";
    private const string SecretField = "clientSecret";
    private const string ApplicationField = "application";
    private const string NativeHash = "clientSecretHash";
    private const string AuditCreated = "ApplicationCreated";
    private const int LifetimeMinutes = 20;
    private const int SecretBytes = 32;
    private const string StatusField = "status";
    private const string TitleField = "title";
    private const string NoSniffHeader = "X-Content-Type-Options";
    private const string NoSniff = "nosniff";
    private const string JsonType = "application/json";
    private static readonly string[] DetailFields = ["clientId", "credential", "displayName", "editable", "expiresAt", "id", "profile", "redirectUris"];
    private static readonly string[] ProblemTitles = ["Invalid application request.", "Sign in required.", "Sign in again to manage applications.",
        "Application access is forbidden.", "Application not found.", "Application conflicts with current registration state.",
        "Reload the application before submitting a new change.", "The application result is unavailable. Do not automatically resubmit."];
    private const string EmptyInput = "{}";
    private const string DuplicatePrefix = "\"clientId\":\"duplicate\",";
    private const string PrivatePrefix = "\"secret\":\"private-canary\",";
    private static readonly string[] ProblemFields = ["type", "title", StatusField, "correlationId", "traceId"];

    /// <summary>Native manager hash, policy and one audit commit through genuine signed-bearer HTTP; later reads never recover the one-time secret.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineHttpsSession_When_ApplicationCreatedThenRead_Then_OneAtomicRegistrationAndSafeDetailsAreReturned(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var response = await test.CreateAsync(Input(test.Now));
        var output = await CreatedAsync(response);
        Assert.Equal(ApplicationVersion.Create(output.Application.Id, 1), response.Headers.ETag!.ToString());
        await RequirePersistedAsync(test, output);
        await RequireReadsAsync(test, output, response.Headers.ETag.ToString());
        using var duplicate = await test.CreateAsync(Input(test.Now));
        await ProblemAsync(duplicate, HttpStatusCode.Conflict);
        Assert.Equal((1, 1), await CountsAsync(test));
    }

    /// <summary>Current database grants, independent of the authenticated bearer, authorize reads and writes separately on every request.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentBearer_When_WriteAndReadGrantsAreRemoved_Then_TheirHttpOperationsAreIndependentlyDenied(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var response = await test.CreateAsync(Input(test.Now));
        var created = await CreatedAsync(response);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == WritePermission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        using var denied = await test.CreateAsync(Input(test.Now, Missing));
        await ProblemAsync(denied, HttpStatusCode.Forbidden);
        using var readable = await test.Browser.GetAsync(Path(created.Application.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == ReadPermission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        using var hidden = await test.Browser.GetAsync(Path(created.Application.Id), TestContext.Current.CancellationToken);
        await ProblemAsync(hidden, HttpStatusCode.Forbidden);
        Assert.Equal((1, 1), await CountsAsync(test));
    }

    /// <summary>Actual API audit persistence failure follows a native internal save yet leaves no registration, policy or successful intent.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeHttpCreate_When_AuditSaveFails_Then_ResponseIsSanitizedAndEntireWriteRollsBack(string provider)
    {
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.AuditFailure);
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, fault);
        fault.Armed = true;
        using var denied = await test.CreateAsync(Input(test.Now), direct: true);
        await ProblemAsync(denied, HttpStatusCode.ServiceUnavailable);
        Assert.True(fault.Fired);
        Assert.Equal((0, 0), await CountsAsync(test));
        await using var scope = test.Scope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreApplication>()
            .AnyAsync(row => row.ClientId == Client, TestContext.Current.CancellationToken));
    }

    /// <summary>Missing bearer, malformed/expired JSON and reserved identity fail through actual HTTP without creating managed state.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CreateHttpBoundary_When_AuthenticationOrInputIsInvalid_Then_NoRegistrationOrAuditIsCreated(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var anonymous = await test.CreateAsync(Input(test.Now), direct: true, authenticated: false);
        await ProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        using var reserved = await test.CreateAsync(Input(test.Now, AdministratorProtocolIdentity.ClientId));
        await ProblemAsync(reserved, HttpStatusCode.Forbidden);
        using var expired = await test.CreateAsync(Input(test.Now.AddMinutes(-LifetimeMinutes)), direct: true);
        await ProblemAsync(expired, HttpStatusCode.BadRequest);
        foreach (var body in new[] { EmptyInput, Input(test.Now).Insert(1, DuplicatePrefix), Input(test.Now).Insert(1, PrivatePrefix) })
        {
            using var malformed = await test.CreateAsync(body, direct: true);
            await ProblemAsync(malformed, HttpStatusCode.BadRequest);
        }
        Assert.Equal((0, 0), await CountsAsync(test));
    }

    /// <summary>Checks native persisted public fields and hash acceptance without including a generated secret or hash in assertion diagnostics.</summary>
    private static async Task RequirePersistedAsync(ApplicationHttpFixture test, ApplicationCredentialOutput output)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var native = await manager.FindByIdAsync(output.Application.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(native);
        Assert.True(await manager.ValidateClientSecretAsync(native, output.ClientSecret, TestContext.Current.CancellationToken));
        var policy = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(output.Application.Id, policy.ApplicationId);
        Assert.Equal(1, policy.Revision);
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.TargetType == AuditTarget, TestContext.Current.CancellationToken);
        Assert.Equal(AuditCreated, audit.Event);
        Assert.Equal(output.Application.Id, audit.TargetId);
        Assert.Equal(test.Environment.Issuer.Account.ActorId, audit.ActorId);
    }

    /// <summary>Both independently authenticated API and BFF details preserve the exact version; collection and later reads omit plaintext and native hashes.</summary>
    private static async Task RequireReadsAsync(ApplicationHttpFixture test, ApplicationCredentialOutput output, string version)
    {
        using var browser = await test.Browser.GetAsync(Path(output.Application.Id), TestContext.Current.CancellationToken);
        using var api = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + output.Application.Id);
        foreach (var response in new[] { browser, api })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(version, response.Headers.ETag!.ToString());
            Assert.True(response.Headers.CacheControl?.NoStore == true);
            Assert.Equal(NoSniff, Assert.Single(response.Headers.GetValues(NoSniffHeader)));
            Assert.Equal(JsonType, response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.False(body.Contains(output.ClientSecret, StringComparison.Ordinal));
            using var json = JsonDocument.Parse(body);
            Assert.Equal(DetailFields, json.RootElement.EnumerateObject().Select(value => value.Name).Order().ToArray());
            Assert.False(json.RootElement.TryGetProperty(SecretField, out _));
            Assert.False(json.RootElement.TryGetProperty(NativeHash, out _));
            var details = JsonSerializer.Deserialize<ApplicationDetails>(body, JsonSerializerOptions.Web)!;
            Assert.Equal(Client, details.ClientId);
            Assert.Equal(Display, details.DisplayName);
            Assert.Equal([Redirect], details.RedirectUris);
            Assert.True(details.Editable);
        }
        using var list = await test.Browser.GetAsync(ApplicationHttpFixture.Collection, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.False((await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains(output.ClientSecret, StringComparison.Ordinal));
    }

    /// <summary>Builds independently named wire fields with an explicit future UTC second.</summary>
    internal static string Input(DateTimeOffset now, string client = Client) => JsonSerializer.Serialize(new
    {
        clientId = client,
        displayName = Display,
        redirectUris = new[] { Redirect },
        credentialExpiresAt = now.AddMinutes(LifetimeMinutes).ToString(ExpiryFormat, CultureInfo.InvariantCulture)
    });

    /// <summary>Reads a successful one-time output in memory only and independently checks secret shape and response safety.</summary>
    internal static async Task<ApplicationCredentialOutput> CreatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(new[] { ApplicationField, SecretField }, json.RootElement.EnumerateObject().Select(value => value.Name).Order().ToArray());
        var result = JsonSerializer.Deserialize<ApplicationCredentialOutput>(body, JsonSerializerOptions.Web)!;
        Assert.Equal(SecretBytes, Convert.FromBase64String(result.ClientSecret).Length);
        return result;
    }

    /// <summary>Checks fixed safe problem fields without outputting response bodies or accepting credential-bearing error members.</summary>
    internal static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.Location);
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal((int)status, json.RootElement.GetProperty(StatusField).GetInt32());
        Assert.All(json.RootElement.EnumerateObject(), field => Assert.Contains(field.Name, ProblemFields));
        Assert.Contains(json.RootElement.GetProperty(TitleField).GetString(), ProblemTitles);
    }

    /// <summary>Counts managed policy and successful application intents independently of response output.</summary>
    internal static async Task<(int Policies, int Audits)> CountsAsync(ApplicationHttpFixture test)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return (await database.Set<ApplicationPolicy>().CountAsync(TestContext.Current.CancellationToken),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.TargetType == AuditTarget, TestContext.Current.CancellationToken));
    }

    /// <summary>Encodes the exact opaque resource into its documented single segment.</summary>
    internal static string Path(string id) => ApplicationHttpFixture.Collection + Separator + Uri.EscapeDataString(id);
}
