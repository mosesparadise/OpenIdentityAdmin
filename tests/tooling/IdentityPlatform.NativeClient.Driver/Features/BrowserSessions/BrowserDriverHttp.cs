using System.Text;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;

/// <summary>Invokes only closed local B3 routes and retains their bounded actual output in private process frames.</summary>
internal static class BrowserDriverHttp
{
    private const string Origin = "Origin";
    private const string Form = "application/x-www-form-urlencoded";
    private const int CookieLimit = 8;
    private const int CookieBytes = 16384;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Uses real middleware and native antiforgery while keeping browser secrets in the private parent/child channel.</summary>
    internal static async Task<NativeClientProcessReply> RunAsync(RequestDelegate application, IServiceProvider root, NativeClientProcessInput input,
        BrowserProcessRequest browser, BrowserDriverTransport transport, CancellationToken token)
    {
        using var encoded = new FormUrlEncodedContent(browser.Operation == BrowserProcessOperation.LogoutSubmit
            ? new Dictionary<string, string> { [BrowserRequestPolicy.AntiforgeryField] = browser.FormToken! } : []);
        var bytes = browser.Operation == BrowserProcessOperation.LogoutSubmit ? await encoded.ReadAsByteArrayAsync(token) : [];
        await using var body = new MemoryStream(bytes, writable: false);
        await using var output = new BrowserDriverBody();
        var http = Context(root, input, browser, body, output, token);
        var completion = new NativeHttpCompletion();
        var lifetime = new NativeRequestLifetime(token);
        http.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        http.Features.Set<IHttpResponseFeature>(completion);
        http.Response.Body = output;
        try
        {
            await application(http);
        }
        finally { await completion.CompleteAsync(); }
        token.ThrowIfCancellationRequested();
        var cookies = http.Response.Headers.SetCookie.Select(value => value ?? throw new InvalidOperationException()).ToArray();
        if (cookies.Length > CookieLimit || cookies.Sum(StrictUtf8.GetByteCount) > CookieBytes)
        {
            throw new InvalidOperationException();
        }
        return new()
        {
            Status = http.Response.StatusCode,
            Aborted = lifetime.Aborted,
            BodyLength = output.Length,
            BrowserBody = StrictUtf8.GetString(output.ToArray()),
            Location = http.Response.Headers.Location.ToString(),
            Cookies = cookies,
            Rejection = string.Empty,
            OutboundRequests = transport.Sends
        };
    }

    /// <summary>Derives method, path and origin solely from the closed operation and fixed validated profile, never arbitrary parent URI input.</summary>
    private static DefaultHttpContext Context(IServiceProvider root, NativeClientProcessInput input, BrowserProcessRequest browser,
        Stream body, Stream output, CancellationToken token)
    {
        var client = new Uri(input.Client);
        var post = browser.Operation == BrowserProcessOperation.LogoutSubmit;
        var http = new DefaultHttpContext { RequestServices = root, RequestAborted = token };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = HostString.FromUriComponent(client);
        http.Request.Method = post ? HttpMethods.Post : HttpMethods.Get;
        http.Request.Path = browser.Operation == BrowserProcessOperation.Status ? BrowserSessionBoundary.Path : BrowserLogoutPage.Path;
        http.Request.Headers.Cookie = input.Cookie;
        http.Request.Headers[Origin] = client.GetLeftPart(UriPartial.Authority);
        http.Request.Body = body;
        http.Request.ContentLength = body.Length;
        http.Request.ContentType = post ? Form : null;
        http.Response.Body = output;
        return http;
    }
}
