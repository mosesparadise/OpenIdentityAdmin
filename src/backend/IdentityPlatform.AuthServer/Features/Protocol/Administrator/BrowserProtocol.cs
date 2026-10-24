using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Marks explicit internal browser protocol composition without mapping product routes or enabling production.</summary>
internal sealed class BrowserProtocol
{
}

/// <summary>Composes completed browser ceremony admission separately from the earlier PAR-only prerequisite.</summary>
internal static class BrowserProtocolSetup
{
    /// <summary>Retains native validation and existing issuance ownership; ordinary unbound B2 requests remain supported.</summary>
    internal static IServiceCollection AddBrowserSessionProtocol(this IServiceCollection services)
    {
        services.AddSingleton<BrowserProtocol>().AddSingleton<BrowserCompletionPar>();
        services.AddOpenIddict().AddServer()
            .AddEventHandler<OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext>(handler => handler
                .UseSingletonHandler<BrowserProtocolRequest>().SetOrder(AdministratorRequestPolicy.AfterNativeValidation + PolicyOffset))
            .AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(handler => handler
                .UseSingletonHandler<BrowserProtocolRequest>().SetOrder(AdministratorRequestPolicy.AfterNativeValidation + PolicyOffset));
        return services;
    }
    private const int PolicyOffset = 500;
}

/// <summary>Checks original B3 input shape before completion ownership; exchange never accepts browser transport overrides.</summary>
internal sealed class BrowserProtocolRequest(AdministratorRegistrationPolicy policy) :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext>,
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    /// <summary>Requires exact raw/native pair agreement and the browser registration before any bound PAR issuance.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext context)
    {
        var http = context.Transaction.GetHttpRequest();
        BrowserLoginBinding? input = null;
        var raw = http is null ? BrowserLoginBindingState.Invalid : BrowserLoginBinding.Read(await http.ReadFormAsync(context.CancellationToken), out input);
        var native = BrowserLoginBinding.Read(context.Request, out var binding);
        if (raw != native || raw == BrowserLoginBindingState.Invalid || raw == BrowserLoginBindingState.Present &&
            (!policy.IsBrowserSession || context.ClientId != AdministratorProtocolIdentity.ClientId || input!.Intent != binding!.Intent || input.Completion != binding.Completion))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }

    /// <summary>Rejects even empty or aliased transport fields at exchange; the original protected code supplies all B3 authority.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        var http = context.Transaction.GetHttpRequest();
        if (http is null || BrowserLoginBinding.Read(await http.ReadFormAsync(context.CancellationToken), out _) != BrowserLoginBindingState.Absent ||
            BrowserLoginBinding.Read(context.Request, out _) != BrowserLoginBindingState.Absent)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }
}
