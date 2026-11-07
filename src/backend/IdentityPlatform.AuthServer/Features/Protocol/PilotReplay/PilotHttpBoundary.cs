using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Captures pilot token responses before authentication without activating administrator-owned storage.</summary>
internal static class PilotHttpBoundary
{
    /// <summary>Starts the standalone request clock before native endpoint inference and any asynchronous form extraction.</summary>
    internal static async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        var request = new ProtocolStorageRequest(http.RequestServices.GetRequiredService<TimeProvider>(), http.RequestAborted);
        if (await ProtocolEndpoint.ResolveAsync(http) == OpenIddictServerEndpointType.Token)
        {
            await using var deadline = new ProtocolResponseDeadline(http, request);
            await RunAsync(http, next, request);
        }
        else
        {
            await next(http);
        }
    }

    /// <summary>Keeps staging and the original watchdog alive through actual callbacks, native cleanup and transport publication.</summary>
    internal static async Task RunAsync(HttpContext http, RequestDelegate next, ProtocolStorageRequest request)
    {
        using var capture = new ProtocolResponseCapture(request);
        var features = new ProtocolResponseFeatures(http, capture);
        var exchange = new PilotExchange(http, request, capture);
        using var ambient = exchange.Enter();
        if (features.Response.HasStarted)
        {
            http.Abort();
            throw ProtocolStorageOwner.Unavailable();
        }
        foreach (var header in features.Response.Headers)
        {
            capture.Headers[header.Key] = header.Value;
        }
        try
        {
            _ = http.Request.BodyReader;
            features.Install();
            await next(http);
            features.RequireIntact(request);
            await capture.SealAsync();
            features.RequireIntact(request);
            exchange.RequirePublication();
            features.Restore();
            await ProtocolResponseTransfer.PublishAsync(features.Response, features.Body, capture, exchange.RequirePublication, request.Token);
        }
        catch
        {
            request.Fail();
            try
            {
                await capture.JoinCallbacksAsync();
            }
            catch { request.Fail(); }
            finally
            {
                features.Restore();
                if (features.Cancellation.IsCancellationRequested || features.Response.HasStarted || request.Remaining <= TimeSpan.Zero)
                {
                    http.Abort();
                }
                else
                {
                    try
                    {
                        await PilotRequestProtection.WriteUnavailableAsync(http, features.Cancellation);
                        await features.Body.CompleteAsync();
                    }
                    catch { http.Abort(); }
                }
            }
        }
        finally { features.Restore(); }
    }
}
