using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Retains and restores the exact HTTP feature graph across bounded native response staging.</summary>
internal sealed class ProtocolResponseFeatures
{
    private readonly HttpContext context;
    private readonly IServiceProvider services;
    private readonly IResponseCookiesFeature? cookies;
    private readonly IHttpUpgradeFeature? upgrade;
    private readonly IHttpWebSocketFeature? sockets;
    private readonly IHttpExtendedConnectFeature? connect;
    private readonly IHttpResponseTrailersFeature? trailers;
    private readonly IResponseCookiesFeature stagedCookies;
    private readonly ProtocolResponseCapture capture;
    internal IHttpResponseFeature Response
    {
        get;
    }
    internal IHttpResponseBodyFeature Body
    {
        get;
    }
    internal CancellationToken Cancellation
    {
        get;
    }

    /// <summary>Snapshots server features before any native staging can materialize cached cookie helpers.</summary>
    internal ProtocolResponseFeatures(HttpContext context, ProtocolResponseCapture capture)
    {
        this.context = context;
        this.capture = capture;
        Response = context.Features.GetRequiredFeature<IHttpResponseFeature>();
        Body = context.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        services = context.RequestServices;
        Cancellation = context.RequestAborted;
        cookies = context.Features.Get<IResponseCookiesFeature>();
        upgrade = context.Features.Get<IHttpUpgradeFeature>();
        sockets = context.Features.Get<IHttpWebSocketFeature>();
        connect = context.Features.Get<IHttpExtendedConnectFeature>();
        trailers = context.Features.Get<IHttpResponseTrailersFeature>();
        stagedCookies = new ResponseCookiesFeature(context.Features);
    }

    /// <summary>Stages standard response APIs, resets cached cookies and removes unsupported streaming upgrades.</summary>
    internal void Install()
    {
        context.Features.Set<IHttpResponseFeature>(capture);
        context.Features.Set<IHttpResponseBodyFeature>(capture);
        context.Features.Set(stagedCookies);
        context.Features.Set<IHttpUpgradeFeature>(null);
        context.Features.Set<IHttpWebSocketFeature>(null);
        context.Features.Set<IHttpExtendedConnectFeature>(null);
        context.Features.Set<IHttpResponseTrailersFeature>(null);
    }

    /// <summary>Rejects replacement of the supported response feature graph before any publication is admitted.</summary>
    internal void RequireIntact(ProtocolStorageRequest request)
    {
        if (!ReferenceEquals(context.Features.Get<IHttpResponseFeature>(), capture) ||
            !ReferenceEquals(context.Features.Get<IHttpResponseBodyFeature>(), capture) ||
            !ReferenceEquals(context.Features.Get<IResponseCookiesFeature>(), stagedCookies) ||
            context.Features.Get<IHttpUpgradeFeature>() is not null || context.Features.Get<IHttpWebSocketFeature>() is not null || context.Features.Get<IHttpExtendedConnectFeature>() is not null || context.Features.Get<IHttpResponseTrailersFeature>() is not null)
        {
            request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Restores original server features only after staged work has stopped using the live context.</summary>
    internal void Restore()
    {
        context.Features.Set(Response);
        context.Features.Set(Body);
        context.Features.Set(cookies);
        context.Features.Set(upgrade);
        context.Features.Set(sockets);
        context.Features.Set(connect);
        context.Features.Set(trailers);
        context.RequestServices = services;
        context.RequestAborted = Cancellation;
    }
}
