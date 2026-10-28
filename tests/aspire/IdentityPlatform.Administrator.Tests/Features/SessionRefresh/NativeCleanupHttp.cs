using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Executes real native ASP.NET form extraction, cryptography and issuance in an isolated component host, not a TLS listener.</summary>
internal static class NativeCleanupHttp
{
    private const string FormType = "application/x-www-form-urlencoded";
    private const string TestAuthentication = "characterization-human-seam";

    /// <summary>Buffers output across the real owned commit and scope disposal; callers may intentionally discard a committed response.</summary>
    internal static Task<NativeCleanupResponse> InvokeAsync(NativeCleanupFixture fixture, string path, Dictionary<string, string> fields, CancellationToken? cancellation = null) =>
        fixture.CommitAsync(async (scope, token) =>
        {
            using var form = new FormUrlEncodedContent(fields);
            await using var input = await form.ReadAsStreamAsync(token);
            await using var output = new MemoryStream();
            var http = Create(scope, path, fields, input, output, token);
            var handler = ActivatorUtilities.CreateInstance<OpenIddictServerAspNetCoreHandler>(scope.Services);
            await handler.InitializeAsync(new AuthenticationScheme(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, null, handler.GetType()), http);
            if (!await handler.HandleRequestAsync())
            {
                await SignInAsync(http, path);
            }
            return new NativeCleanupResponse(http.Response.StatusCode, output.ToArray(), http.Response.Headers.Location.ToString());
        }, cancellation);

    /// <summary>Supplies actual URL/form bytes while preserving the original owned services and cancellation token.</summary>
    private static DefaultHttpContext Create(ProtocolStorageScope scope, string path, Dictionary<string, string> fields,
        Stream input, Stream output, CancellationToken token)
    {
        var http = new DefaultHttpContext { RequestServices = scope.Services, RequestAborted = token };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = new HostString(new Uri(NativeCleanupProfile.Issuer).Host);
        http.Request.Path = path;
        http.Response.Body = output;
        http.Request.Method = path == NativeCleanupProfile.Authorize ? HttpMethods.Get : HttpMethods.Post;
        if (HttpMethods.IsGet(http.Request.Method))
        {
            http.Request.QueryString = QueryString.Create(fields.Select(pair => KeyValuePair.Create<string, string?>(pair.Key, pair.Value)));
        }
        else
        {
            http.Request.ContentType = FormType;
            http.Request.ContentLength = input.Length;
            http.Request.Body = input;
        }
        return http;
    }

    /// <summary>Uses an explicit test human-identity seam only at authorization; code/refresh exchanges require native token authentication.</summary>
    private static async Task SignInAsync(HttpContext http, string path)
    {
        ClaimsPrincipal principal;
        if (path == NativeCleanupProfile.Authorize)
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity(TestAuthentication))
                .SetClaim(OpenIddictConstants.Claims.Subject, NativeCleanupProfile.Subject)
                .SetScopes(http.GetOpenIddictServerRequest()!.GetScopes());
        }
        else
        {
            Assert.Equal(NativeCleanupProfile.Token, path);
            var result = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            Assert.True(result.Succeeded);
            principal = result.Principal!;
        }
        await http.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);
    }
}

/// <summary>Keeps synthetic protocol bytes in memory and prevents a generated credential-bearing ToString.</summary>
internal sealed class NativeCleanupResponse(int status, byte[] body, string location)
{
    internal int Status => status;
    internal byte[] Body => body;
    internal string Location => location;
}
