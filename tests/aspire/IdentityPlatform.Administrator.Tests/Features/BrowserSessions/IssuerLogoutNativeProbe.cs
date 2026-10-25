using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Mutates only the selected real native boundary while retaining exact effect and response witnesses.</summary>
internal sealed class IssuerLogoutNativeProbe(IssuerLogoutNativeFault mode)
{
    internal const string Foreign = "https://foreign.example.test/";
    internal const string PrivateMarker = "synthetic-private-native-logout-marker";
    private OpenIddictServerEvents.ValidateEndSessionRequestContext? validation;
    internal IssuerLogoutFault Fault { get; } = new();
    internal int Observed
    {
        get; private set;
    }

    /// <summary>Captures the real validation and installs faults only on the selected actual native event.</summary>
    internal void Configure(IServiceCollection services)
    {
        Fault.Configure(services);
        var server = services.AddOpenIddict().AddServer();
        server.AddEventHandler<OpenIddictServerEvents.ValidateEndSessionRequestContext>(handler => handler.SetOrder(int.MaxValue).UseInlineHandler(context =>
        {
            validation = context;
            return ValueTask.CompletedTask;
        }));
        if (mode == IssuerLogoutNativeFault.ResponseField)
        {
            server.AddEventHandler<OpenIddictServerEvents.ApplyEndSessionResponseContext>(handler => handler
                .SetOrder(OpenIddictServerHandlers.Session.AttachResponseState.Descriptor.Order + 1).UseInlineHandler(context =>
                {
                    Observed++;
                    context.Response.AccessToken = PrivateMarker;
                    return ValueTask.CompletedTask;
                }));
        }
        if (mode == IssuerLogoutNativeFault.LateOutput)
        {
            server.AddEventHandler<OpenIddictServerEvents.ProcessSignOutContext>(handler => handler.SetOrder(int.MinValue).UseInlineHandler(context =>
            {
                var http = context.Transaction.GetHttpRequest()!.HttpContext;
                http.Response.OnStarting(() => { Observed++; http.Response.Headers.Location = Foreign; return Task.CompletedTask; });
                return ValueTask.CompletedTask;
            }));
        }
    }

    /// <summary>Changes the separately validated native destination only after the real logout save has completed.</summary>
    internal void Arm()
    {
        if (mode == IssuerLogoutNativeFault.ValidatedDestination)
        {
            Fault.AfterSave = () =>
            {
                var original = validation!.Request.PostLogoutRedirectUri;
                validation.Request.PostLogoutRedirectUri = Foreign;
                validation.SetPostLogoutRedirectUri(Foreign);
                validation.Request.PostLogoutRedirectUri = original;
                Observed++;
            };
        }
    }
}
