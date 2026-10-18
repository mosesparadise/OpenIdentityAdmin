using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Injects one unplanned field immediately before the real formatter, after native credential generation.</summary>
internal sealed class NativeAccountResponseFault
{
    internal const string AuthorizationRefresh = "authorization-refresh";
    internal const string AuthorizationIdentity = "authorization-identity";
    internal const string TokenRefresh = "token-refresh";
    internal const string TokenIdentity = "token-identity";
    private const string IdentityField = "email";
    private const string Injected = "synthetic-unplanned-response";
    internal string? Selected
    {
        get; set;
    }
    internal int Observed
    {
        get; private set;
    }

    /// <summary>Retains the ordinary native pipeline and inserts only the explicit test mutation at its formatter boundary.</summary>
    internal static void Configure(OpenIddictServerBuilder server)
    {
        server.Services.AddSingleton<NativeAccountResponseFault>();
        server.AddEventHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>(handler => handler
            .SetOrder(OpenIddictServerAspNetCoreHandlers.Authentication.ProcessQueryResponse.Descriptor.Order - 1).UseInlineHandler(Apply));
        server.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(handler => handler
            .SetOrder(OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<OpenIddictServerEvents.ApplyTokenResponseContext>.Descriptor.Order - 1).UseInlineHandler(Apply));
    }

    /// <summary>Mutates only the chosen successful response and records actual reach without exposing response values.</summary>
    private static ValueTask Apply(OpenIddictServerEvents.BaseRequestContext context)
    {
        var control = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices.GetRequiredService<NativeAccountResponseFault>();
        var authorization = control.Selected is AuthorizationRefresh or AuthorizationIdentity;
        if (control.Selected is not null && string.IsNullOrEmpty(context.Transaction.Response?.Error) &&
            context.EndpointType == (authorization ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token))
        {
            var field = control.Selected is AuthorizationRefresh or TokenRefresh ? OpenIddictConstants.Parameters.RefreshToken : IdentityField;
            context.Transaction.Response!.SetParameter(field, Injected);
            control.Observed++;
        }
        return ValueTask.CompletedTask;
    }
}
