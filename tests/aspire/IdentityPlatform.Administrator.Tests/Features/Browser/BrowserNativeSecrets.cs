using System.Net;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Captures actual server-only native credentials privately while preserving the original HTTPS pipeline and response objects.</summary>
internal sealed class BrowserNativeSecrets
{
    private const string Par = "/connect/par";
    private const string Token = "/connect/token";
    private const string Assertion = "client_assertion";
    private const string Verifier = "code_verifier";
    private const string Access = "access_token";
    private const string Identity = "id_token";
    private const int MaximumBytes = 262144;
    private const int ExpectedSecrets = 5;
    private readonly List<string> secrets = [];
    private int par;
    private int token;
    private int issued;

    /// <summary>Adds a fresh passive delegating handler without replacing the native primary handler, retries, TLS or handler lifetime.</summary>
    internal void Configure(IServiceCollection services) => services.ConfigureAll<HttpClientFactoryOptions>(options =>
        options.HttpMessageHandlerBuilderActions.Add(builder => builder.AdditionalHandlers.Add(
            new Capture(this, builder.Services.GetRequiredService<NativeClientProfile>().Issuer))));

    /// <summary>Fails closed unless the genuine one-PAR/one-exchange flow provided every expected nonempty secret.</summary>
    internal string[] Require()
    {
        Assert.Equal(1, par);
        Assert.Equal(1, token);
        Assert.Equal(1, issued);
        var values = secrets.ToArray();
        Assert.Equal(ExpectedSecrets, values.Length);
        Assert.True(values.All(value => !string.IsNullOrEmpty(value)));
        return values;
    }

    /// <summary>Extracts only actual native form fields from bounded buffered content, never logging or replacing them.</summary>
    private async Task RequestAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        await request.Content!.LoadIntoBufferAsync(MaximumBytes, cancellation);
        var form = QueryHelpers.ParseQuery(await request.Content.ReadAsStringAsync(cancellation));
        secrets.Add(One(form, Assertion));
        if (request.RequestUri!.AbsolutePath == Par)
        {
            par++;
        }
        else
        {
            token++;
            secrets.Add(One(form, Verifier));
        }
    }

    /// <summary>Captures actual issued credentials after the original native network send, retaining the same buffered response.</summary>
    private async Task ResponseAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.Content.LoadIntoBufferAsync(MaximumBytes, cancellation);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        secrets.Add(json.RootElement.GetProperty(Access).GetString()!);
        secrets.Add(json.RootElement.GetProperty(Identity).GetString()!);
        issued++;
    }

    /// <summary>Requires one canonical field without rendering its value or surrounding form on failure.</summary>
    private static string One(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> form, string name)
    {
        Assert.True(form.TryGetValue(name, out var value) && value.Count == 1 && !string.IsNullOrEmpty(value[0]));
        return value[0]!;
    }

    /// <summary>Observes only exact native POST endpoints and calls the original transport once with unchanged cancellation.</summary>
    private sealed class Capture(BrowserNativeSecrets owner, Uri issuer) : DelegatingHandler
    {
        /// <summary>Buffers only bounded test observations; observation failures become fixed safe failures rather than silent empty evidence.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var selected = request.Method == HttpMethod.Post && request.RequestUri is { } uri && uri.Scheme == Uri.UriSchemeHttps &&
                uri.GetLeftPart(UriPartial.Authority) == issuer.GetLeftPart(UriPartial.Authority) && uri.AbsolutePath is Par or Token;
            try
            {
                if (selected)
                {
                    await owner.RequestAsync(request, cancellationToken);
                }
                var response = await base.SendAsync(request, cancellationToken);
                try
                {
                    if (selected && request.RequestUri!.AbsolutePath == Token)
                    {
                        await owner.ResponseAsync(response, cancellationToken);
                    }
                }
                catch { response.Dispose(); throw; }
                return response;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw new InvalidOperationException(Failure); }
        }
        private const string Failure = "Native private browser-evidence capture failed.";
    }
}
