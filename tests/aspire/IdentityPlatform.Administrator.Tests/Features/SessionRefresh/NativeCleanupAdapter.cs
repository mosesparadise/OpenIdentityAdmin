using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Characterizes a private selector extension while retaining native client proof and endpoint-permission validation.</summary>
internal static class NativeCleanupAdapter
{
    /// <summary>Recognizes only the dedicated test hint; ordinary RFC7009 requests retain all installed native handlers.</summary>
    internal static bool Selected(OpenIddictRequest request) => request.TokenTypeHint == NativeCleanupProfile.SelectorType;

    /// <summary>Replaces credential-target stages only; a selector is never promoted to a validated token principal.</summary>
    internal static void Configure(OpenIddictServerBuilder server)
    {
        server.Services.AddSingleton<NativeCleanupOrdinary>();
        Filter<OpenIddictServerEvents.ValidateRevocationRequestContext>(server, OpenIddictServerHandlers.Revocation.ValidateTokenType.Descriptor);
        Filter<OpenIddictServerEvents.ValidateRevocationRequestContext>(server, OpenIddictServerHandlers.Revocation.ValidateAuthorizedParty.Descriptor);
        Filter<OpenIddictServerEvents.HandleRevocationRequestContext>(server, OpenIddictServerHandlers.Revocation.AttachPrincipal.Descriptor);
        Filter<OpenIddictServerEvents.HandleRevocationRequestContext>(server, OpenIddictServerHandlers.Revocation.RevokeToken.Descriptor);
        server.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.EvaluateValidatedTokens.Descriptor.Order + NativeCleanupProfile.OrderOffset)
            .UseInlineHandler(context =>
            {
                if (context.EndpointType == OpenIddictServerEndpointType.Revocation && Selected(context.Request))
                {
                    context.ExtractGenericToken = context.RequireGenericToken = context.ValidateGenericToken = context.RejectGenericToken = false;
                }
                return ValueTask.CompletedTask;
            }));
        server.AddEventHandler<OpenIddictServerEvents.ValidateRevocationRequestContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Revocation.ValidateEndpointPermissions.Descriptor.Order + NativeCleanupProfile.OrderOffset)
            .UseScopedHandler<NativeCleanupSelection>());
        server.AddEventHandler<OpenIddictServerEvents.HandleRevocationRequestContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Revocation.RevokeToken.Descriptor.Order + NativeCleanupProfile.OrderOffset)
            .UseScopedHandler<NativeCleanupRevocation>());
        server.AddEventHandler<OpenIddictServerEvents.ApplyRevocationResponseContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Revocation.NormalizeErrorResponse.Descriptor.Order - NativeCleanupProfile.OrderOffset)
            .UseInlineHandler(context =>
            {
                context.Transaction.GetHttpRequest()!.HttpContext.Items[NativeCleanupHttp.ErrorKey] = context.Error;
                return ValueTask.CompletedTask;
            }));
        server.AddEventHandler<OpenIddictServerEvents.ApplyRevocationResponseContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Revocation.NormalizeErrorResponse.Descriptor.Order + NativeCleanupProfile.OrderOffset)
            .UseInlineHandler(NativeCleanupRevocation.ConfirmAsync));
    }
    /// <summary>Imports the installed descriptor unchanged except for the exact selector exclusion filter.</summary>
    private static void Filter<TContext>(OpenIddictServerBuilder server, OpenIddictServerHandlerDescriptor descriptor)
        where TContext : OpenIddictServerEvents.BaseContext
    {
        server.RemoveEventHandler(descriptor);
        server.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<TContext>().Import(descriptor)
            .AddFilter<NativeCleanupOrdinary>().Build());
    }

}

/// <summary>Retains stock target validation for every request outside the explicit characterization extension.</summary>
internal sealed class NativeCleanupOrdinary : IOpenIddictServerHandlerFilter<OpenIddictServerEvents.BaseContext>
{
    /// <summary>Filters target handlers without changing client assertion, application lookup or endpoint admission.</summary>
    public ValueTask<bool> IsActiveAsync(OpenIddictServerEvents.BaseContext context) =>
        new(context.Transaction.Request is not { } request || !NativeCleanupAdapter.Selected(request));
}
