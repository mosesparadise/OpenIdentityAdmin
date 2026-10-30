using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Retains the legacy pilot transport while owning all enabled administrator/discovery native requests before authentication.</summary>
internal static class ProtocolHosting
{
    /// <summary>Classifies with actual native URI semantics after shared account limits and before authentication resolves handlers.</summary>
    internal static async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        var endpoint = await ProtocolEndpoint.ResolveAsync(http);
        var profiles = http.RequestServices.GetRequiredService<ProtocolProfiles>();
        var request = http.RequestServices.GetRequiredService<ProtocolHostRequest>();
        if (endpoint == OpenIddictServerEndpointType.Unknown && profiles.Managed && http.GetEndpoint()?.Metadata.GetMetadata<
            IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications.ManagedAccountEndpoint>() is not null)
        {
            await IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications.ManagedAccountBoundary.RunAsync(http, next);
        }
        else if (endpoint == OpenIddictServerEndpointType.Unknown)
        {
            var account = profiles.Pilot && http.GetEndpoint()?.Metadata.GetMetadata<PilotAccountEndpoint>() is not null;
            request.Bind(http, endpoint, account ? ProtocolHostMode.PilotAccount : ProtocolHostMode.Unknown);
            if (account)
            {
                http.RequestServices.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
            }
            await next(http);
        }
        else
        {
            await RunConfiguredAsync(http, next, endpoint, profiles, request);
        }
    }

    /// <summary>Starts the original hard watchdog before asynchronous form classification and retains it through actual completion.</summary>
    private static async Task RunConfiguredAsync(HttpContext http, RequestDelegate next, OpenIddictServerEndpointType endpoint,
        ProtocolProfiles profiles, ProtocolHostRequest host)
    {
        var request = new ProtocolStorageRequest(http.RequestServices.GetRequiredService<TimeProvider>(), http.RequestAborted);
        await using var deadline = new ProtocolResponseDeadline(http, request);
        if (profiles.Pilot && await PilotTransportIdentity.MatchesAsync(http, endpoint))
        {
            request.RequireCurrent();
            host.Bind(http, endpoint, ProtocolHostMode.PilotProtocol);
            http.RequestServices.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
            await next(http);
            request.RequireCurrent();
        }
        else
        {
            await RunOwnedAsync(http, next, endpoint, request);
        }
    }

    /// <summary>Installs the marker in the actual fresh scope used by authentication, native stores and endpoint decisions.</summary>
    private static Task RunOwnedAsync(HttpContext http, RequestDelegate next, OpenIddictServerEndpointType endpoint, ProtocolStorageRequest original) =>
        ProtocolHttpBoundary.RunAsync(http, http.RequestServices.GetRequiredService<IServiceScopeFactory>(),
            original, async (_, scope, _) =>
            {
                var request = scope.Services.GetRequiredService<ProtocolHostRequest>();
                request.Bind(http, endpoint, ProtocolHostMode.Owned);
                await next(http);
                return Claim(http, request, endpoint);
            });

    /// <summary>Requires actual native completion and the private administrator issuance permit before admitting success bytes.</summary>
    private static ProtocolResponseOutcome Claim(HttpContext http, ProtocolHostRequest request, OpenIddictServerEndpointType endpoint)
    {
        var outer = request.Outer ?? throw ProtocolStorageOwner.Unavailable();
        request.RequireInferred(outer);
        if (endpoint == OpenIddictServerEndpointType.EndSession && IssuerLogoutFlow.Find(outer.Transaction) is { } logout)
        {
            return logout.Claim(outer);
        }
        if (ManagedEntryPublication.Claim(http, outer))
        {
            return ProtocolResponseOutcome.Committed;
        }
        if (ManagedCacheCoordinator.Claim(http, outer))
        {
            return ProtocolResponseOutcome.Committed;
        }
        if (NativeErrorCoordinator.Claim(http, outer))
        {
            return ProtocolResponseOutcome.VerifiedNativeError;
        }
        if (outer.IsRejected)
        {
            return ProtocolResponseOutcome.NativeError;
        }
        if (endpoint is OpenIddictServerEndpointType.Configuration or OpenIddictServerEndpointType.JsonWebKeySet)
        {
            return outer.IsRequestHandled && !outer.IsRequestSkipped ? ProtocolResponseOutcome.ReadOnly : throw ProtocolStorageOwner.Unavailable();
        }
        if (endpoint == OpenIddictServerEndpointType.PushedAuthorization)
        {
            AdministratorIssuanceCoordinator.ClaimPublication(http, outer);
        }
        else
        {
            AdministratorIssuanceCoordinator.ClaimEndpointPublication(http);
        }
        return ProtocolResponseOutcome.Committed;
    }
}
