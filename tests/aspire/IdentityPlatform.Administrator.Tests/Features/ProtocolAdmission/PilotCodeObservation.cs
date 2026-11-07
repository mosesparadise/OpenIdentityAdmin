using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Captures one genuine protected code principal after native redemption without pausing issuance or modifying protocol state.</summary>
internal sealed class PilotCodeObservation
{
    private const string Pilot = "identityplatform-pilot";
    internal ClaimsPrincipal Principal { get; private set; } = null!;
    internal bool Armed { get; set; } = true;

    /// <summary>Observes the installed native redemption slot while preserving every production handler.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddServer()
        .AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order + 1).UseInlineHandler(ObserveAsync));

    /// <summary>Freezes only the authenticated original code input; future exchanges cannot replace this observation.</summary>
    private ValueTask ObserveAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (Armed && context.ClientId == Pilot && context.EndpointType == OpenIddictServerEndpointType.Token && context.Request.IsAuthorizationCodeGrantType())
        {
            var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
                typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
            Assert.NotNull(authentication?.AuthorizationCodePrincipal);
            Principal = authentication.AuthorizationCodePrincipal.Clone();
            Assert.False(string.IsNullOrEmpty(Principal.GetTokenId()));
            Armed = false;
        }
        return ValueTask.CompletedTask;
    }
}
