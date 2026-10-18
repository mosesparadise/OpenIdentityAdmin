using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Uses the pinned native URI resolution and endpoint inference for middleware routing, including native URI aliases.</summary>
internal static class ProtocolEndpoint
{
    /// <summary>Performs no extraction, validation or manager resolution; the actual native request independently repeats inference.</summary>
    internal static async ValueTask<OpenIddictServerEndpointType> ResolveAsync(HttpContext http)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Options = http.RequestServices.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue,
            Logger = NullLogger.Instance,
            CancellationToken = http.RequestAborted
        };
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        var context = new OpenIddictServerEvents.ProcessRequestContext(transaction);
        await new OpenIddictServerAspNetCoreHandlers.ResolveRequestUri().HandleAsync(context);
        await new OpenIddictServerHandlers.InferEndpointType().HandleAsync(context);
        return context.EndpointType;
    }
}
