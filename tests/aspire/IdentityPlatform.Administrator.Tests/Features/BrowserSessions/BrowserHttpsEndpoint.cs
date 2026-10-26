using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Retains a bound loopback socket until its real Kestrel host has stopped and every request has completed.</summary>
internal sealed class BrowserHttpsEndpoint : IDisposable
{
    private const string EndSessionPath = "/connect/end-session";
    private const string ParPath = "/connect/par";
    private const string TokenPath = "/connect/token";
    private const string HostName = "localhost";
    private const string AttackerPath = "/__test/cross-site-form";
    private const string AttackerPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action ";
    private const string AttackerPrefix = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Synthetic cross-site form</title></head><body>";
    private const string AttackerSuffix = "</body></html>";
    private const string Html = "text/html; charset=utf-8";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string StrictOrigin = "strict-origin";
    private const string NoStore = "no-store";
    private const int CleanupSeconds = 30;
    private readonly Socket socket = SocketTransportOptions.CreateDefaultBoundListenSocket(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly ConcurrentDictionary<long, TaskCompletionSource> pending = new();
    private int claimed;
    private long requests;
    private int par;
    private int token;
    private int endSessionGets;
    private int endSessionPosts;
    private int endSessionRedirects;
    private AttackerDocument? attacker;
    private ResponseProbe? responseProbe;
    internal BrowserResponseWitness? ResponseWitness
    {
        get; set;
    }
    internal BrowserReturnFailure ReturnFailure { get; } = new();
    internal BrowserReturnFailure CallbackFailure { get; } = new(CallbackPath);
    private const string CallbackPath = "/callback";
    internal BrowserNavigationPause Navigation { get; } = new();
    internal BrowserStatusObservation Status { get; } = new();
    internal Uri Address
    {
        get;
    }
    internal BrowserHttpsPolicy Policy
    {
        get;
    }
    internal int ParRequests => Volatile.Read(ref par);
    internal int TokenRequests => Volatile.Read(ref token);
    internal int EndSessionGets => Volatile.Read(ref endSessionGets);
    internal int EndSessionPosts => Volatile.Read(ref endSessionPosts);
    internal int EndSessionRedirects => Volatile.Read(ref endSessionRedirects);

    /// <summary>Fixes the public identity before any credential, registration or account profile is constructed, without releasing the port.</summary>
    internal BrowserHttpsEndpoint(bool ipAddress = false)
    {
        Address = new UriBuilder(Uri.UriSchemeHttps, ipAddress ? IPAddress.Loopback.ToString() : HostName, ((IPEndPoint)socket.LocalEndPoint!).Port).Uri;
        Policy = new(Address);
    }

    /// <summary>Transfers this exact managed socket to Kestrel once, retaining ordinary development-certificate TLS validation.</summary>
    internal void Configure(IWebHostBuilder builder)
    {
        builder.UseSockets(options => options.CreateBoundListenSocket = endpoint =>
        {
            Assert.True(endpoint.Equals(socket.LocalEndPoint));
            Assert.Equal(0, Interlocked.Exchange(ref claimed, 1));
            return socket;
        });
        builder.UseKestrel(options => options.Listen(IPAddress.Loopback, Address.Port, listener => listener.UseHttps()));
    }

    /// <summary>Counts actual backchannel requests and joins completion without retaining secret-bearing HTTP contexts or payloads.</summary>
    internal async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        var id = Interlocked.Increment(ref requests);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(pending.TryAdd(id, completion));
        try
        {
            if (http.Request.Path == ParPath)
            {
                Interlocked.Increment(ref par);
            }
            if (http.Request.Path == TokenPath)
            {
                Interlocked.Increment(ref token);
            }
            if (http.Request.Path == EndSessionPath)
            {
                if (HttpMethods.IsGet(http.Request.Method))
                {
                    Interlocked.Increment(ref endSessionGets);
                }
                if (HttpMethods.IsPost(http.Request.Method))
                {
                    Interlocked.Increment(ref endSessionPosts);
                }
            }
            Policy.Request(http.Request);
            Status.Visit(http.Request);
            http.Response.OnStarting(async () =>
            {
                await Navigation.VisitAsync(http, response: true);
                ObserveResponse(http);
            });
            await Navigation.VisitAsync(http, response: false);
            if (!ReturnFailure.Abort(http, Address) && !CallbackFailure.Abort(http, Address) && !ServeResponseProbe(http) && !await ServeAttackerAsync(http))
            {
                await next(http);
            }
        }
        finally
        {
            Assert.True(pending.TryRemove(id, out _));
            completion.TrySetResult();
        }
    }

    /// <summary>Counts final native confirmation redirects at the actual fixed issuer endpoint, independently of browser response-event classification.</summary>
    private void ObserveResponse(HttpContext http)
    {
        Policy.Response(http);
        Status.Response(http);
        ResponseWitness?.Observe(http);
        if (http.Request.Path == EndSessionPath && HttpMethods.IsPost(http.Request.Method) && http.Response.StatusCode == StatusCodes.Status302Found)
        {
            Interlocked.Increment(ref endSessionRedirects);
        }
    }

    /// <summary>Stages one real redirect with a deleted synthetic cookie before the existing one-shot TLS document.</summary>
    internal Uri StageResponseProbe(string cookie, Uri target)
    {
        Assert.True(target.GetLeftPart(UriPartial.Authority) == Address.GetLeftPart(UriPartial.Authority));
        Assert.True(Interlocked.CompareExchange(ref responseProbe, new(cookie, target), null) is null);
        return new Uri(Address, ResponseProbePath);
    }

    /// <summary>Serves only the isolated test-owned canary; its expired cookie must not be returned by the browser.</summary>
    private bool ServeResponseProbe(HttpContext http)
    {
        if (http.Request.Path != ResponseProbePath || !HttpMethods.IsGet(http.Request.Method))
        {
            return false;
        }
        var probe = Interlocked.Exchange(ref responseProbe, null);
        Assert.NotNull(probe);
        http.Response.Headers.SetCookie = ProbeCookiePrefix + probe.Cookie + ProbeCookieSuffix;
        http.Response.Headers.Location = probe.Target.AbsoluteUri;
        http.Response.Headers.CacheControl = NoStore;
        http.Response.StatusCode = StatusCodes.Status302Found;
        return true;
    }

    /// <summary>Stages one test-owned sender document with only the fixed peer authority permitted; product routes and policies remain untouched.</summary>
    internal Uri StageAttacker(string form, Uri target, bool fetch = false)
    {
        var document = new AttackerDocument(form, target.GetLeftPart(UriPartial.Authority), fetch);
        Assert.True(Interlocked.CompareExchange(ref attacker, document, null) is null);
        return new Uri(Address, AttackerPath);
    }

    /// <summary>Serves the one-shot sender before account middleware, retaining actual TLS, browser-origin calculation and SameSite enforcement.</summary>
    private async Task<bool> ServeAttackerAsync(HttpContext http)
    {
        if (http.Request.Path != AttackerPath || !HttpMethods.IsGet(http.Request.Method))
        {
            return false;
        }
        var document = Interlocked.Exchange(ref attacker, null);
        Assert.NotNull(document);
        http.Response.ContentType = Html;
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = StrictOrigin;
        http.Response.Headers.ContentSecurityPolicy = AttackerPolicy + document.Target + (document.Fetch ? ConnectPolicy + document.Target : string.Empty);
        await http.Response.WriteAsync(AttackerPrefix + document.Form + AttackerSuffix, http.RequestAborted);
        return true;
    }

    /// <summary>Stops admission before joining in-flight work; dependencies remain alive through server completion even after stop cancellation.</summary>
    internal async Task StopAsync(IHost host)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await host.StopAsync(cleanup.Token);
        }
        finally
        {
            while (!pending.IsEmpty)
            {
                await Task.WhenAll(pending.Values.Select(value => value.Task));
            }
        }
    }

    /// <summary>Releases an unclaimed socket after startup failure or the same already-stopped managed socket after host disposal.</summary>
    public void Dispose() => socket.Dispose();

    /// <summary>Retains synthetic form content in memory only until its single response is served.</summary>
    private sealed record AttackerDocument(string Form, string Target, bool Fetch);
    private sealed record ResponseProbe(string Cookie, Uri Target);
    private const string ResponseProbePath = "/__test/response-cookie";
    private const string ProbeCookiePrefix = "__Host-response-probe=";
    private const string ProbeCookieSuffix = "; Max-Age=0; Path=/; Secure; HttpOnly; SameSite=Lax";
    private const string ConnectPolicy = "; connect-src ";
}
