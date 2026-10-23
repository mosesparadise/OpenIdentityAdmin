using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Test-only representation transfer after real B2 account admission; supplies no completed ceremony or B3 claim authority.</summary>
internal sealed class NativeBrowserBindingProbe(bool transfer)
{
    private const int BetweenHandlers = 500;
    internal BrowserLoginBinding? Restored
    {
        get; private set;
    }
    internal bool Copied
    {
        get; private set;
    }
    internal bool Exchanged
    {
        get; private set;
    }

    /// <summary>Retains every native handler and production owner; only the test root receives the explicit private-data transfer.</summary>
    internal void Configure(IServiceCollection services)
    {
        var server = services.AddOpenIddict().AddServer();
        server.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.RestoreParametersFromRequestToken.Descriptor.Order + BetweenHandlers)
            .UseInlineHandler(ObserveRestoration));
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.EvaluateGeneratedTokens.Descriptor.Order - BetweenHandlers)
            .UseInlineHandler(ObserveIssuance));
    }

    /// <summary>Reads the actual restored native request after protected PAR validation rather than copying outer authorization parameters.</summary>
    private ValueTask ObserveRestoration(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization)
        {
            Assert.NotNull(context.RequestTokenPrincipal);
            Assert.Equal(OpenIddictConstants.RequestTokenTypes.Private.PushedAuthorizationRequest,
                context.RequestTokenPrincipal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType));
            Assert.Equal(BrowserLoginBindingState.Present, BrowserLoginBinding.Read(context.Request, out var binding));
            Restored = binding;
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Copies only test data before native code preparation, then observes its survival through the real exchange projection.</summary>
    private ValueTask ObserveIssuance(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization && transfer)
        {
            Assert.NotNull(context.Principal?.GetClaim(OpenIddictConstants.Claims.Subject));
            Assert.NotNull(Restored);
            Restored.CopyTo(context.Principal!);
            Copied = true;
        }
        if (context.EndpointType == OpenIddictServerEndpointType.Token)
        {
            Assert.NotNull(Restored);
            Assert.Equal(transfer, Restored.Matches(context.Principal!));
            Exchanged = true;
        }
        return ValueTask.CompletedTask;
    }
}
