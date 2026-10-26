using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Retains only scalar observations of actual Kestrel form headers and protocol navigation, never request values.</summary>
internal sealed class BrowserHttpsPolicy(Uri address)
{
    private const string RootPath = "/";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string FormReferrer = "strict-origin";
    private const string NoReferrer = "no-referrer";
    private static readonly string[] FormPaths = ["/bff/login", "/bff/logout", "/account/administrator/browser/login", "/account/administrator/browser/mfa", "/connect/end-session"];
    private static readonly string[] ReturnPaths = ["/bff/login/resume", "/callback", "/bff/logout/return", "/connect/authorize"];
    private readonly string origin = address.GetLeftPart(UriPartial.Authority);
    private int posts;
    private int returns;
    private int originFailures;
    private int formReferrerFailures;
    private int returnReferrerFailures;
    private int responsePolicyFailures;
    private int forms;
    private int redirects;

    /// <summary>Observes the original server request before any parser can normalize its native headers.</summary>
    internal void Request(HttpRequest request)
    {
        if (HttpMethods.IsPost(request.Method) && FormPaths.Contains(request.Path.Value, StringComparer.Ordinal))
        {
            Interlocked.Increment(ref posts);
            if (request.Headers.Origin.Count != 1 || request.Headers.Origin[0] != origin)
            {
                Interlocked.Increment(ref originFailures);
            }
            if (request.Headers.Referer.Count != 1 || request.Headers.Referer[0] != origin + RootPath)
            {
                Interlocked.Increment(ref formReferrerFailures);
            }
        }
        if (HttpMethods.IsGet(request.Method) && ReturnPaths.Contains(request.Path.Value, StringComparer.Ordinal))
        {
            Interlocked.Increment(ref returns);
            if (request.Headers.Referer.Count != 0)
            {
                Interlocked.Increment(ref returnReferrerFailures);
            }
        }
    }

    /// <summary>Checks final actual response policy on successful form GETs and every observed protocol redirect.</summary>
    internal void Response(HttpContext http)
    {
        var form = HttpMethods.IsGet(http.Request.Method) && http.Response.StatusCode == StatusCodes.Status200OK &&
            FormPaths.Contains(http.Request.Path.Value, StringComparer.Ordinal);
        var redirect = http.Response.StatusCode is StatusCodes.Status302Found or StatusCodes.Status303SeeOther;
        if (form)
        {
            Interlocked.Increment(ref forms);
        }
        if (redirect)
        {
            Interlocked.Increment(ref redirects);
        }
        if ((form || redirect) && http.Response.Headers[ReferrerPolicy] != (form ? FormReferrer : NoReferrer))
        {
            Interlocked.Increment(ref responsePolicyFailures);
        }
    }

    /// <summary>Requires the exact expected native operations, preventing a silent observer or missing navigation from passing.</summary>
    internal void Require(int expectedPosts, int expectedReturns, int expectedForms, int expectedRedirects)
    {
        Assert.Equal(expectedPosts, Volatile.Read(ref posts));
        Assert.Equal(expectedReturns, Volatile.Read(ref returns));
        Assert.Equal(expectedForms, Volatile.Read(ref forms));
        Assert.Equal(expectedRedirects, Volatile.Read(ref redirects));
        Assert.Equal(0, Volatile.Read(ref originFailures));
        Assert.Equal(0, Volatile.Read(ref formReferrerFailures));
        Assert.Equal(0, Volatile.Read(ref returnReferrerFailures));
        Assert.Equal(0, Volatile.Read(ref responsePolicyFailures));
    }
}
