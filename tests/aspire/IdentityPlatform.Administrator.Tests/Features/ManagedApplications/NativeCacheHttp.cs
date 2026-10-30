using System.Security.Claims;
using System.Text;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Runs native HTTP extraction, caching and cryptography in an owned component host, without claiming a TLS listener or real human login.</summary>
internal static class NativeCacheHttp
{
    private const string FormType = "application/x-www-form-urlencoded";
    private const string TestAuthentication = "cache-characterization-human-consent-seam";
    private const string BasicPrefix = "Basic ";

    /// <summary>Allows the initial native anonymous caching branch to finish naturally; human sign-in is supplied only after native passthrough.</summary>
    internal static Task<NativeCacheResponse> InvokeAsync(NativeCacheFixture fixture, string path, Dictionary<string, string> fields,
        bool consent = false) => fixture.CommitAsync<NativeCacheResponse>(async (scope, token) =>
    {
        using var form = new FormUrlEncodedContent(fields);
        await using var input = await form.ReadAsStreamAsync(token);
        await using var output = new MemoryStream();
        var http = Create(scope, path, fields, input, output, fixture.Secret, token);
        var handler = ActivatorUtilities.CreateInstance<OpenIddictServerAspNetCoreHandler>(scope.Services);
        await handler.InitializeAsync(new AuthenticationScheme(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, null, handler.GetType()), http);
        var handled = await handler.HandleRequestAsync();
        ClaimsPrincipal? cached = null;
        if (!handled)
        {
            var authentication = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            cached = authentication.Properties?.GetParameter<ClaimsPrincipal>(OpenIddictServerAspNetCoreConstants.Properties.RequestTokenPrincipal);
            if (consent || path == NativeCacheProfile.Token)
            {
                await SignInAsync(http, path, authentication);
            }
        }
        return new(http.Response.StatusCode, output.ToArray(), http.Response.Headers.Location.ToString(), handled,
            http.GetOpenIddictServerRequest(), cached, http.GetOpenIddictServerResponse()?.Error, http.Response.ContentType);
    });

    /// <summary>Supplies actual query/form/Basic bytes; the native extractor owns client credential interpretation.</summary>
    private static DefaultHttpContext Create(ProtocolStorageScope scope, string path, Dictionary<string, string> fields,
        Stream input, Stream output, string secret, CancellationToken token)
    {
        var http = new DefaultHttpContext { RequestServices = scope.Services, RequestAborted = token };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = new HostString(new Uri(NativeCacheProfile.Issuer).Host);
        http.Request.Path = path;
        http.Response.Body = output;
        http.Request.Method = path == NativeCacheProfile.Authorize ? HttpMethods.Get : HttpMethods.Post;
        if (HttpMethods.IsGet(http.Request.Method))
        {
            http.Request.QueryString = QueryString.Create(fields.Select(pair => KeyValuePair.Create<string, string?>(pair.Key, pair.Value)));
        }
        else
        {
            http.Request.ContentType = FormType;
            http.Request.ContentLength = input.Length;
            http.Request.Body = input;
            http.Request.Headers.Authorization = BasicPrefix + Convert.ToBase64String(Encoding.ASCII.GetBytes(NativeCacheProfile.Client + ':' + secret));
        }
        return http;
    }

    /// <summary>Labels the authorization-only synthetic seam explicitly; token exchange can use only actual native code authentication.</summary>
    private static async Task SignInAsync(HttpContext http, string path, AuthenticateResult authentication)
    {
        ClaimsPrincipal principal;
        if (path == NativeCacheProfile.Authorize)
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity(TestAuthentication))
                .SetClaim(OpenIddictConstants.Claims.Subject, NativeCacheProfile.Subject)
                .SetScopes(http.GetOpenIddictServerRequest()!.GetScopes());
        }
        else
        {
            Assert.Equal(NativeCacheProfile.Token, path);
            Assert.True(authentication.Succeeded);
            principal = authentication.Principal!;
        }
        await http.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);
    }
}

/// <summary>Retains synthetic protocol bytes only in memory without a generated credential-bearing diagnostic representation.</summary>
internal sealed class NativeCacheResponse(int status, byte[] body, string location, bool handled, OpenIddictRequest? request, ClaimsPrincipal? cached, string? error, string? contentType)
{
    internal string? Error => error;
    internal string? ContentType => contentType;
    internal int Status => status;
    internal byte[] Body => body;
    internal string Location => location;
    internal bool Handled => handled;
    internal OpenIddictRequest? Request => request;
    internal ClaimsPrincipal? Cached => cached;
}
