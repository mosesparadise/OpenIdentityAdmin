using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Preserves installed native descriptors while adding operation ownership at the actual backchannel send boundary.</summary>
internal static class NativeClientTransportOwnership
{
    /// <summary>Replaces only the PAR/token mutation send slots; discovery/JWKS retain their separately pinned metadata policy.</summary>
    internal static void Add(OpenIddictClientBuilder client)
    {
        var par = OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor;
        client.RemoveEventHandler(par);
        client.Services.AddSingleton<OpenIddictClientHandlers.SendPushedAuthorizationRequest>();
        client.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessChallengeContext>()
            .Import(par).UseScopedHandler<NativeClientSendPar>().SetType(OpenIddictClientHandlerType.Custom).Build());
        var token = OpenIddictClientHandlers.SendTokenRequest.Descriptor;
        client.RemoveEventHandler(token);
        client.Services.AddSingleton<OpenIddictClientHandlers.SendTokenRequest>();
        client.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessAuthenticationContext>()
            .Import(token).UseScopedHandler<NativeClientSendToken>().SetType(OpenIddictClientHandlerType.Custom).Build());
        Replace<OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext>(client);
        Replace<OpenIddictClientEvents.ApplyTokenRequestContext>(client);
    }

    /// <summary>Retains original HTTP filters/order and invokes the installed sender only after inspecting its actual message.</summary>
    private static void Replace<TContext>(OpenIddictClientBuilder client) where TContext : OpenIddictClientEvents.BaseExternalContext
    {
        var descriptor = OpenIddictClientSystemNetHttpHandlers.SendHttpRequest<TContext>.Descriptor;
        client.RemoveEventHandler(descriptor);
        client.Services.AddSingleton<OpenIddictClientSystemNetHttpHandlers.SendHttpRequest<TContext>>();
        client.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<TContext>().Import(descriptor)
            .UseSingletonHandler<NativeClientHttpSend<TContext>>().SetType(OpenIddictClientHandlerType.Custom).Build());
    }
}
