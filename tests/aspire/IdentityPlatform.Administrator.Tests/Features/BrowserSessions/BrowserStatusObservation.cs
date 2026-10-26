using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Counts actual cross-origin status requests and cookie/header presence without retaining their values.</summary>
internal sealed class BrowserStatusObservation
{
    private const string Status = "/bff/session";
    private const string Cookie = "__Host-identity-admin-session";
    private int crossOrigin;
    private int authenticatedCookies;
    private int bearer;
    private int completed;
    private int wrongPeer;
    private int invalidResponse;
    internal string? PeerOrigin
    {
        private get; set;
    }
    internal int CrossOrigin => Volatile.Read(ref crossOrigin);
    internal int AuthenticatedCookies => Volatile.Read(ref authenticatedCookies);
    internal int Bearer => Volatile.Read(ref bearer);
    internal int Completed => Volatile.Read(ref completed);
    internal int WrongPeer => Volatile.Read(ref wrongPeer);
    internal int InvalidResponse => Volatile.Read(ref invalidResponse);

    /// <summary>Observes only actual status GETs before application dispatch, never synthesizing browser headers or authority.</summary>
    internal void Visit(HttpRequest request)
    {
        if (request.Path != Status || !HttpMethods.IsGet(request.Method))
        {
            return;
        }
        if (request.Headers.Origin.Count > 0)
        {
            Interlocked.Increment(ref crossOrigin);
            if (request.Headers.Origin.Count != 1 || request.Headers.Origin[0] != PeerOrigin)
            {
                Interlocked.Increment(ref wrongPeer);
            }
            if (request.Cookies.ContainsKey(Cookie))
            {
                Interlocked.Increment(ref authenticatedCookies);
            }
        }
        if (request.Headers.Authorization.Count > 0)
        {
            Interlocked.Increment(ref bearer);
        }
    }

    /// <summary>Requires the actual cross-origin response to succeed without CORS opt-in, distinguishing denial from a network/server failure.</summary>
    internal void Response(HttpContext http)
    {
        if (http.Request.Path != Status || !HttpMethods.IsGet(http.Request.Method) || http.Request.Headers.Origin.Count == 0)
        {
            return;
        }
        Interlocked.Increment(ref completed);
        if (http.Response.StatusCode != StatusCodes.Status200OK || http.Response.Headers.ContainsKey(AllowOrigin) ||
            http.Response.Headers.ContainsKey(AllowCredentials))
        {
            Interlocked.Increment(ref invalidResponse);
        }
    }
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";
}
