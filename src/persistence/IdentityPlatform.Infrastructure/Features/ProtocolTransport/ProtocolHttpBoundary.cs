using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Owns a dormant nonstreaming protocol invocation through response admission and actual cleanup.</summary>
internal static class ProtocolHttpBoundary
{
    private const string FailureCacheControl = "no-store";

    /// <summary>Aborts transport at the original deadline but never detaches work that still references its HTTP context.</summary>
    internal static Task RunAsync(HttpContext context, IServiceScopeFactory factory, TimeProvider clock,
        Func<HttpContext, ProtocolStorageScope, CancellationToken, ValueTask<ProtocolResponseOutcome>> operation) =>
        RunAsync(context, factory, new ProtocolStorageRequest(clock, context.RequestAborted), operation);

    /// <summary>Retains a budget started before asynchronous host classification so entering owned execution cannot renew the request.</summary>
    internal static async Task RunAsync(HttpContext context, IServiceScopeFactory factory, ProtocolStorageRequest request,
        Func<HttpContext, ProtocolStorageScope, CancellationToken, ValueTask<ProtocolResponseOutcome>> operation)
    {
        using var capture = new ProtocolResponseCapture(request);
        var features = new ProtocolResponseFeatures(context, capture);
        var response = features.Response;
        await using var deadline = new ProtocolResponseDeadline(context, request);
        if (response.HasStarted)
        {
            context.Abort();
            throw ProtocolStorageOwner.Unavailable();
        }
        foreach (var header in response.Headers)
        {
            capture.Headers[header.Key] = header.Value;
        }
        try
        {
            // Framework input-pipe cleanup belongs to the original host, before any owned child response/services exist.
            _ = context.Request.BodyReader;
            features.Install();
            var outcome = await InvokeAsync(context, factory, operation, request, capture, features);
            await request.DrainAsync();
            request.RequirePublication(outcome);
            features.Restore();
            if (outcome == ProtocolResponseOutcome.NativeError)
            {
                await RejectAsync(response, features.Body, features.Cancellation);
            }
            else
            {
                await PublishAsync(response, features.Body, capture, request, outcome);
            }
        }
        catch
        {
            request.Fail();
            try
            {
                await request.DrainAsync();
            }
            finally
            {
                features.Restore();
                if (deadline.Expired || features.Cancellation.IsCancellationRequested || response.HasStarted)
                {
                    context.Abort();
                }
                else
                {
                    await RejectAsync(response, features.Body, features.Cancellation);
                }
            }
        }
        finally { features.Restore(); }
    }

    /// <summary>Keeps native callbacks and their owned services together before the request owner begins disposal.</summary>
    private static Task<ProtocolResponseOutcome> InvokeAsync(HttpContext context, IServiceScopeFactory factory,
        Func<HttpContext, ProtocolStorageScope, CancellationToken, ValueTask<ProtocolResponseOutcome>> operation,
        ProtocolStorageRequest request, ProtocolResponseCapture capture, ProtocolResponseFeatures features) => request.RunAsync(factory, async (scope, token) =>
    {
        context.RequestServices = scope.Services;
        context.RequestAborted = token;
        try
        {
            var outcome = await operation(context, scope, token);
            features.RequireIntact(request);
            await capture.SealAsync();
            features.RequireIntact(request);
            request.RequirePublication(outcome);
            return outcome;
        }
        finally { await capture.JoinCallbacksAsync(); }
    });

    /// <summary>Transfers only an admitted finite response and keeps the original watchdog active throughout actual sending.</summary>
    private static async Task PublishAsync(IHttpResponseFeature response, IHttpResponseBodyFeature body,
        ProtocolResponseCapture capture, ProtocolStorageRequest request, ProtocolResponseOutcome outcome)
    {
        request.RequirePublication(outcome);
        response.StatusCode = capture.StatusCode;
        response.ReasonPhrase = capture.ReasonPhrase;
        response.Headers.Clear();
        foreach (var header in capture.Headers)
        {
            response.Headers[header.Key] = header.Value;
        }
        await body.StartAsync(request.Token);
        request.RequirePublication(outcome);
        await body.Stream.WriteAsync(capture.Bytes, request.Token);
        await body.Stream.FlushAsync(request.Token);
        await body.CompleteAsync();
        request.RequirePublication(outcome);
    }

    /// <summary>Publishes no staged success material when native rejection has no verified response permit.</summary>
    private static async Task RejectAsync(IHttpResponseFeature response, IHttpResponseBodyFeature body, CancellationToken token)
    {
        response.Headers.Clear();
        response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        response.ReasonPhrase = null;
        response.Headers.CacheControl = FailureCacheControl;
        await body.StartAsync(token);
        await body.Stream.FlushAsync(token);
        await body.CompleteAsync();
    }

}
