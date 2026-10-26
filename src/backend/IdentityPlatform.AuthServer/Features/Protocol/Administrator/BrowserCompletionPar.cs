using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Explicit dormant PAR adapter; registering it does not enable browser routes or authorize a completed login.</summary>
internal sealed class BrowserCompletionPar(AdministratorRegistrationPolicy policy)
{
    /// <summary>Validates original cardinality and protected native scalar values after normal client admission and before owned attachment.</summary>
    internal async ValueTask<IssuerParCompletion?> OpenAsync(ProtocolStorageScope scope,
        OpenIddictServerEvents.ProcessSignInContext context, CancellationToken token)
    {
        var http = context.Transaction.GetHttpRequest() ?? throw ProtocolStorageOwner.Unavailable();
        var present = http.Headers.ContainsKey(BrowserAuditCorrelation.Header);
        var correlation = BrowserAuditCorrelation.Read(http.Headers[BrowserAuditCorrelation.Header]);
        var form = await http.ReadFormAsync(token);
        if (present != http.Headers.ContainsKey(BrowserAuditCorrelation.Header) ||
            correlation != BrowserAuditCorrelation.Read(http.Headers[BrowserAuditCorrelation.Header]))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var raw = BrowserLoginBinding.Read(form, out var input);
        var native = BrowserLoginBinding.Read(context.Request, out var binding);
        if (raw == BrowserLoginBindingState.Absent && native == BrowserLoginBindingState.Absent &&
            !present)
        {
            return null;
        }
        if (correlation is null || !policy.IsBrowserSession || raw != BrowserLoginBindingState.Present || native != BrowserLoginBindingState.Present ||
            input!.Intent != binding!.Intent || input.Completion != binding.Completion)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return await IssuerParCompletion.OpenAsync(scope, binding, correlation.Value,
            () => BrowserAuditCorrelation.Read(http.Headers[BrowserAuditCorrelation.Header]) == correlation, token);
    }
}

/// <summary>Registers only the bounded completion prerequisite; current product composition never invokes it.</summary>
internal static class BrowserCompletionParSetup
{
    /// <summary>Enables test-owned native PAR attachment and denies B3-bound authorization until exact session matching is implemented.</summary>
    internal static IServiceCollection AddBrowserCompletionPar(this IServiceCollection services)
    {
        services.AddSingleton<BrowserCompletionPar>();
        services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.RestoreParametersFromRequestToken.Descriptor.Order + GuardOrderOffset)
            .UseInlineHandler(context =>
            {
                if (context.EndpointType == OpenIddictServerEndpointType.Authorization &&
                    BrowserLoginBinding.Read(context.Request, out _) != BrowserLoginBindingState.Absent)
                {
                    context.Reject(OpenIddict.Abstractions.OpenIddictConstants.Errors.InvalidRequest);
                }
                return default;
            }));
        return services;
    }
    private const int GuardOrderOffset = 500;
}
