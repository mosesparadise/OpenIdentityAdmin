using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Checks the actual embedded editor and generated HTTP contract through genuine TLS hosts and both databases; no browser rendering is claimed.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationWorkspaceTests(BootstrapFixture fixture)
{
    private const string HtmlMedia = "text/html";
    private const string ScriptMedia = "text/javascript";
    private const string StyleMedia = "text/css";
    private const string MissingRepository = "Repository root is unavailable.";
    private const string RepositoryMarker = "Directory.Build.props";
    private const string Workspace = "/applications";
    private const string Assets = "/admin-assets/";
    private const string Source = "src/frontend/IdentityPlatform.Admin.Ui/generated";
    private const string Script = "app.js";
    private const string Style = "app.css";
    private const string Document = "index.html";
    private const string SecretField = "clientSecret";
    private const string ApplicationField = "application";
    private const string IdentityField = "id";
    private const string NoSniff = "nosniff";
    private const string NoSniffHeader = "X-Content-Type-Options";
    private const string PolicyHeader = "Content-Security-Policy";
    private const string ScriptPolicy = "script-src 'self'";
    private const string FormPolicy = "form-action 'self'";
    private static readonly string[] EditorText = ["Create application", "Application details", "Save your client secret now", "Secret expiry (UTC)"];

    /// <summary>Served bytes match the reviewed deterministic assets while real CSRF creation returns one secret and subsequent details retain only sanitized data.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AuthenticatedWorkspace_When_EditorAssetsAndCreateDetailsAreRequested_Then_ReviewedBytesAndOneTimeContractAreServed(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, workspace: true);
        await RequireAssetAsync(test.Browser, Workspace, Document, HtmlMedia);
        var script = await RequireAssetAsync(test.Browser, Assets + Script, Script, ScriptMedia);
        Assert.All(EditorText, text => Assert.Contains(text, Encoding.UTF8.GetString(script), StringComparison.Ordinal));
        await RequireAssetAsync(test.Browser, Assets + Style, Style, StyleMedia);
        using var created = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var output = await ApplicationHttpTests.CreatedAsync(created);
        using var response = await test.Browser.GetAsync(ApplicationHttpTests.Path(output.Application.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Headers.ETag, response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.False(body.Contains(output.ClientSecret, StringComparison.Ordinal));
        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.TryGetProperty(SecretField, out _));
        Assert.False(json.RootElement.TryGetProperty(ApplicationField, out _));
        Assert.Equal(output.Application.Id, json.RootElement.GetProperty(IdentityField).GetString());
        Assert.Equal((1, 1), await ApplicationHttpTests.CountsAsync(test));
    }

    /// <summary>Compares the HTTPS response to independent generated files, so a stale embedded assembly cannot satisfy this check.</summary>
    private static async Task<byte[]> RequireAssetAsync(HttpClient browser, string path, string file, string media)
    {
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(media, response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(NoSniff, Assert.Single(response.Headers.GetValues(NoSniffHeader)));
        var policy = Assert.Single(response.Headers.GetValues(PolicyHeader));
        Assert.Contains(ScriptPolicy, policy, StringComparison.Ordinal);
        Assert.Contains(FormPolicy, policy, StringComparison.Ordinal);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var source = await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), Source, file), TestContext.Current.CancellationToken);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(source)), Convert.ToHexString(SHA256.HashData(bytes)));
        return bytes;
    }

    /// <summary>Locates the checked-out source without depending on a test runner's current working directory.</summary>
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)) && Directory.Exists(Path.Combine(directory.FullName, Source)))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException(MissingRepository);
    }
}
