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
    private const int CleanupSeconds = 30;
    private readonly Socket socket = SocketTransportOptions.CreateDefaultBoundListenSocket(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly ConcurrentDictionary<long, TaskCompletionSource> pending = new();
    private int claimed;
    private long requests;
    private int par;
    private int token;
    private int endSessionGets;
    private int endSessionPosts;
    internal Uri Address
    {
        get;
    }
    internal int ParRequests => Volatile.Read(ref par);
    internal int TokenRequests => Volatile.Read(ref token);
    internal int EndSessionGets => Volatile.Read(ref endSessionGets);
    internal int EndSessionPosts => Volatile.Read(ref endSessionPosts);

    /// <summary>Fixes the public identity before any credential, registration or account profile is constructed, without releasing the port.</summary>
    internal BrowserHttpsEndpoint() => Address = new UriBuilder(Uri.UriSchemeHttps, HostName, ((IPEndPoint)socket.LocalEndPoint!).Port).Uri;

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
            await next(http);
        }
        finally
        {
            Assert.True(pending.TryRemove(id, out _));
            completion.TrySetResult();
        }
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
}
