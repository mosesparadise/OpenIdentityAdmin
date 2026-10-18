using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Isolates a native continuation's response and callbacks until its actual owned child cleanup completes.</summary>
internal sealed class ProtocolResponseChild
{
    private readonly HttpContext context;
    private readonly ProtocolStorageRequest request;
    private readonly ProtocolResponseCapture capture;
    private readonly ProtocolResponseFeatures features;
    private ProtocolStorageScope? scope;
    private bool sealedOutput;
    private Task? seal;

    /// <summary>Requires the original bounded parent response; a nested continuation never writes directly to live transport.</summary>
    private ProtocolResponseChild(HttpContext context, ProtocolStorageRequest request, ProtocolResponseCapture capture)
    {
        this.context = context;
        this.request = request;
        this.capture = capture;
        features = new(context, capture);
        if (features.Response is not ProtocolResponseCapture parent || !parent.BelongsTo(request) || !ReferenceEquals(features.Body, parent) || parent.HasStarted)
        {
            request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        foreach (var header in parent.Headers)
        {
            capture.Headers[header.Key] = header.Value;
        }
    }

    /// <summary>Returns immutable sealed output only after callback, transaction and child-service cleanup; an unsealed rejection returns no output.</summary>
    internal static async ValueTask<(T Result, ProtocolResponseOutput? Output)> RunAsync<T>(HttpContext context, IServiceScopeFactory factory,
        Func<ProtocolStorageScope, ProtocolResponseChild, CancellationToken, ValueTask<T>> operation)
    {
        var parent = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        using var capture = new ProtocolResponseCapture(parent.Request);
        var stage = new ProtocolResponseChild(context, parent.Request, capture);
        try
        {
            var result = await ProtocolStorageChild.RunAsync(factory, async (scope, token) =>
            {
                stage.scope = scope;
                stage.features.Install();
                context.RequestServices = scope.Services;
                context.RequestAborted = token;
                try
                {
                    var value = await operation(scope, stage, token);
                    stage.features.RequireIntact(parent.Request);
                    return value;
                }
                finally
                {
                    try
                    {
                        if (stage.seal is not null)
                        {
                            await stage.seal;
                        }
                    }
                    finally { await capture.JoinCallbacksAsync(); }
                }
            });
            stage.features.RequireIntact(parent.Request);
            return (result, stage.sealedOutput ? new(context, parent, (ProtocolResponseCapture)stage.features.Response, capture) : null);
        }
        finally { stage.features.Restore(); }
    }

    /// <summary>Drains native producers while the exact child still owns its scoped services, before the caller's final authority check and commit.</summary>
    internal Task SealAsync()
    {
        if (scope is null || !ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, scope) ||
            !ReferenceEquals(ProtocolStorageAmbient.Current?.Request, request) || !ReferenceEquals(context.RequestServices, scope.Services))
        {
            request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        features.RequireIntact(request);
        var completion = capture.SealAsync();
        return seal ??= ObserveSealAsync(completion);
    }

    /// <summary>Retains even a caller-abandoned seal task through the child operation's actual finally cleanup.</summary>
    private async Task ObserveSealAsync(Task completion)
    {
        await completion;
        features.RequireIntact(request);
        scope!.Owner.RequireHealthy();
        sealedOutput = true;
    }
}
