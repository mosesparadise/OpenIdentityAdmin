using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Runs raw native issuer requests over trusted loopback HTTPS, with finite test-only transport interventions.</summary>
internal sealed class NativeIssuerHttpsHost : IAsyncDisposable
{
    internal enum Fault
    {
        None, ParLoss, TokenLoss, ParUnavailable, TokenUnavailable, ParRedirect, TokenRedirect
    }
    private const int CleanupSeconds = 30;
    private const string LoopbackName = "localhost";
    private const string RedirectPath = "/unexpected-redirect";
    private readonly ConcurrentDictionary<long, TaskCompletionSource> pending = new();
    private readonly WebApplication application;
    private AdministratorProtocolScenario? scenario;
    private long nextRequest;
    private int parSends;
    private int tokenSends;
    private int redirectedSends;
    private int losses;
    private int failures;
    internal Fault Selected
    {
        get; set;
    }
    internal Uri Issuer { get; private set; } = null!;
    internal long Requests => Interlocked.Read(ref nextRequest);
    internal int ParSends => Volatile.Read(ref parSends);
    internal int TokenSends => Volatile.Read(ref tokenSends);
    internal int RedirectedSends => Volatile.Read(ref redirectedSends);
    internal int Failures => Volatile.Read(ref failures);
    internal int Losses => Volatile.Read(ref losses);

    /// <summary>Retains the live application before startup so every failure can dispose its listener.</summary>
    private NativeIssuerHttpsHost(WebApplication application) => this.application = application;

    /// <summary>Reserves a real dynamic listener using the existing trusted development certificate; native TLS validation remains enabled.</summary>
    internal static async Task<NativeIssuerHttpsHost> StartAsync(X509Certificate2? certificate = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listener =>
        {
            if (certificate is null)
            {
                listener.UseHttps();
            }
            else
            {
                listener.UseHttps(certificate);
            }
        }));
        var host = new NativeIssuerHttpsHost(builder.Build());
        host.application.Run(host.InvokeAsync);
        try
        {
            await host.application.StartAsync(TestContext.Current.CancellationToken);
            var address = new Uri(host.application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            host.Issuer = new UriBuilder(Uri.UriSchemeHttps, LoopbackName, address.Port).Uri;
            return host;
        }
        catch { await host.DisposeAsync(); throw; }
    }

    /// <summary>Publishes exactly one fully initialized issuer; requests before binding receive only a fixed unavailable status.</summary>
    internal void Bind(AdministratorProtocolScenario value)
    {
        Assert.Equal(Issuer, value.Issuer);
        Assert.Null(Interlocked.CompareExchange(ref scenario, value, null));
    }

    /// <summary>Tracks actual server completion independently of a client observing response headers or connection loss.</summary>
    private async Task InvokeAsync(HttpContext http)
    {
        var id = Interlocked.Increment(ref nextRequest);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(pending.TryAdd(id, completion));
        try
        {
            var issuer = Volatile.Read(ref scenario);
            if (issuer is null)
            {
                http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
            await DispatchAsync(http, issuer);
        }
        catch
        {
            Interlocked.Increment(ref failures);
            throw;
        }
        finally
        {
            Assert.True(pending.TryRemove(id, out _));
            completion.TrySetResult();
        }
    }

    /// <summary>Passes original Kestrel bytes through real production ownership and native dispatch under an independent issuer scope.</summary>
    private async Task DispatchAsync(HttpContext http, AdministratorProtocolScenario issuer)
    {
        var path = http.Request.Path.Value!;
        Count(path);
        var fault = Selected;
        if (Reject(http, path, fault))
        {
            return;
        }
        var originalServices = http.RequestServices;
        var originalBody = http.Features.Get<IHttpResponseBodyFeature>()!;
        await using var scope = issuer.Services.CreateAsyncScope();
        http.RequestServices = scope.ServiceProvider;
        NativeLostResponse? loss = null;
        if (fault == Fault.ParLoss && path == AdministratorProtocolScenario.ParPath ||
            fault == Fault.TokenLoss && path == AdministratorProtocolScenario.TokenPath)
        {
            loss = new NativeLostResponse(http, originalBody, () => Interlocked.Increment(ref losses));
            http.Features.Set<IHttpResponseBodyFeature>(loss);
        }
        try
        {
            await ProtocolHosting.InvokeAsync(http, context => DispatchNativeAsync(context, path));
        }
        catch (Exception error) when (loss?.Caused(error) == true) { }
        finally
        {
            http.RequestServices = originalServices;
            http.Features.Set(originalBody);
        }
    }

    /// <summary>Retains unexpected native-handler failure reach even when the production boundary deliberately suppresses exception details.</summary>
    private async Task DispatchNativeAsync(HttpContext http, string path)
    {
        try
        {
            await NativeAccountHttp.DispatchAsync(http, path, new NativeAccountResult());
        }
        catch
        {
            Interlocked.Increment(ref failures);
            throw;
        }
    }

    /// <summary>Injects finite pre-processing failures or a same-server redirect target; any unintended follow is independently counted.</summary>
    private bool Reject(HttpContext http, string path, Fault fault)
    {
        var par = path == AdministratorProtocolScenario.ParPath;
        var token = path == AdministratorProtocolScenario.TokenPath;
        if (par && fault == Fault.ParUnavailable || token && fault == Fault.TokenUnavailable)
        {
            http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return true;
        }
        if (par && fault == Fault.ParRedirect || token && fault == Fault.TokenRedirect)
        {
            http.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            http.Response.Headers.Location = new Uri(Issuer, RedirectPath).AbsoluteUri;
            return true;
        }
        if (path == RedirectPath)
        {
            Interlocked.Increment(ref redirectedSends);
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return true;
        }
        return false;
    }

    /// <summary>Counts only observed mutation requests, without retaining headers, body, query or protocol artifacts.</summary>
    private void Count(string path)
    {
        if (path == AdministratorProtocolScenario.ParPath)
        {
            Interlocked.Increment(ref parSends);
        }
        else if (path == AdministratorProtocolScenario.TokenPath)
        {
            Interlocked.Increment(ref tokenSends);
        }
    }

    /// <summary>Joins all server work after the caller has joined its client operations; no delayed request is abandoned.</summary>
    internal async Task DrainAsync(CancellationToken? cancellation = null)
    {
        while (!pending.IsEmpty)
        {
            await Task.WhenAll(pending.Values.Select(value => value.Task)).WaitAsync(cancellation ?? TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Stops accepting requests and joins transport before dependent issuer services leave their enclosing scope.</summary>
    internal async Task StopAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await application.StopAsync(cleanup.Token);
        }
        finally
        {
            await DrainAsync(CancellationToken.None);
        }
    }

    /// <summary>Stops and drains transport before the enclosing fixture releases issuer services, keys or database leases.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally { await application.DisposeAsync(); }
    }
}
