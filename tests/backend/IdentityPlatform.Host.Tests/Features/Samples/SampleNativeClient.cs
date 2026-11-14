using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using IdentityPlatform.Sample.Client.Composition;
using IdentityPlatform.Sample.Client.Features.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Owns a real sample TestServer, private setup file and browser cookie jar without tokens in browser script or URLs.</summary>
internal sealed class SampleNativeClient : IAsyncDisposable
{
    private const string DevelopmentEnvironment = "Development";
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string TrueValue = "true";
    private const string ConfigurationFilename = "sample.json";
    private const string ClientIdSampleTestClientSecretSyntheticSecretText = "{\"clientId\":\"sample-test\",\"clientSecret\":\"synthetic-secret\"}";
    private const string CookieHeader = "Cookie";
    private const string OriginHeader = "Origin";
    private const string SetCookieHeader = "Set-Cookie";
    private const string HomePath = "/";
    private const string AntiforgeryPattern = "name=\"([^\"]+)\" value=\"([^\"]+)\"";
    private const int TokenValueGroup = 2;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "sample-client-" + Guid.NewGuid().ToString("N"));
    private readonly CookieContainer cookies = new();
    internal WebApplication App
    {
        get;
    }
    private HttpClient? client;
    internal HttpClient Client => client ??= CreateClient();

    /// <summary>Composes production middleware and native handlers, substituting only private file location and external HTTP responses.</summary>
    internal SampleNativeClient(SampleNativeIssuer issuer, Action<IServiceCollection>? configure = null)
    {
        Directory.CreateDirectory(directory);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = DevelopmentEnvironment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [EnabledSetting] = TrueValue });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new SampleConfiguration(Path.Combine(directory, ConfigurationFilename)));
        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(value => value.AdditionalHandlers.Add(issuer.Handler())));
        configure?.Invoke(builder.Services);
        App = SampleApplication.Create(builder);
    }

    /// <summary>Creates browser transport only after the real TestServer has started.</summary>
    private HttpClient CreateClient()
    {
        var value = App.GetTestClient();
        value.BaseAddress = new Uri(SampleNativeIssuer.Client);
        return value;
    }

    /// <summary>Performs the exact private file publication used by Docker stdin, with synthetic credentials only.</summary>
    internal async Task ConfigureAsync()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(ClientIdSampleTestClientSecretSyntheticSecretText));
        await SampleConfiguration.ConfigureAsync(input, TestContext.Current.CancellationToken, Path.Combine(directory, ConfigurationFilename));
    }

    /// <summary>Sends an ordinary browser request and preserves native secure cookies, never exposing their protected payloads in assertions.</summary>
    internal Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null, string? origin = SampleNativeIssuer.Client) =>
        SendAsync(method, path, TestContext.Current.CancellationToken, content, origin);

    /// <summary>Sends a separately cancellable browser request for native publication-race tests.</summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, CancellationToken cancellationToken, HttpContent? content = null, string? origin = SampleNativeIssuer.Client)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.TryAddWithoutValidation(CookieHeader, cookies.GetCookieHeader(Client.BaseAddress!));
        if (method == HttpMethod.Post && origin is not null)
        {
            request.Headers.TryAddWithoutValidation(OriginHeader, origin.TrimEnd('/'));
        }
        var response = await Client.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues(SetCookieHeader, out var values))
        {
            foreach (var value in values)
            {
                cookies.SetCookies(Client.BaseAddress!, value);
            }
        }
        return response;
    }

    /// <summary>Reads a fresh native antiforgery pair under the current browser identity before a protected POST.</summary>
    internal async Task<HttpResponseMessage> PostAsync(string path)
    {
        using var form = await FormAsync();
        return await SendAsync(HttpMethod.Post, path, form);
    }

    /// <summary>Captures a native antiforgery form before a separately ordered concurrent publication request.</summary>
    internal async Task<FormUrlEncodedContent> FormAsync()
    {
        using var page = await SendAsync(HttpMethod.Get, HomePath);
        var body = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = Regex.Match(body, AntiforgeryPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(match.Success);
        return new FormUrlEncodedContent(new Dictionary<string, string> { [WebUtility.HtmlDecode(match.Groups[1].Value)] = WebUtility.HtmlDecode(match.Groups[TokenValueGroup].Value) });
    }

    /// <summary>Disposes the actual host, HTTP transport and private file after each isolated native scenario.</summary>
    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await App.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }
}
