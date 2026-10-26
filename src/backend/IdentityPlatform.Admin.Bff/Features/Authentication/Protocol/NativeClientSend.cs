using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Owns the actual native PAR service call and carries its private binding into the final native HTTP sender.</summary>
internal sealed class NativeClientSendPar(OpenIddictClientHandlers.SendPushedAuthorizationRequest native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessChallengeContext>
{
    /// <summary>Retains native error/response handling and forbids another send after response loss or cancellation.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        var continuation = NativeClientContinuation.Find(context);
        continuation.Require(context);
        return NativeClientBackchannel.RunAsync(context.PushedAuthorizationRequest!, context.Registration, context.Configuration,
            context.PushedAuthorizationEndpoint!, () => continuation.Require(context), () => native.HandleAsync(context), NativeBrowserChallenge.Find(context)?.Correlation);
    }
}

/// <summary>Owns the actual native token service call after durable callback consumption and native assertion generation.</summary>
internal sealed class NativeClientSendToken(OpenIddictClientHandlers.SendTokenRequest native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Retains one native intentional exchange and rechecks state lifetime before accepting its result.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        var callback = NativeClientCallback.Find(context);
        callback.RequireToken(context);
        return NativeClientBackchannel.RunAsync(context.TokenRequest!, context.Registration, context.Configuration,
            context.TokenEndpoint!, () => callback.RequireToken(context), () => native.HandleAsync(context));
    }
}

/// <summary>Wraps the installed SystemNetHttp send handler itself, preserving real native serialization and networking.</summary>
internal sealed class NativeClientHttpSend<TContext>(OpenIddictClientSystemNetHttpHandlers.SendHttpRequest<TContext> native)
    : IOpenIddictClientHandler<TContext> where TContext : OpenIddictClientEvents.BaseExternalContext
{
    /// <summary>Requires the one-shot operation permit immediately before delegating to the actual pinned native sender.</summary>
    public ValueTask HandleAsync(TContext context) => NativeClientBackchannel.SendAsync(context, () => native.HandleAsync(context));
}
