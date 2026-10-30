using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.Authorize;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.AuthServer.Features.Protocol.Token;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Dispatches fixed-profile decisions before resolving profile-specific application services.</summary>
internal static class ProtocolDecisions
{
    /// <summary>Routes only the actual skipped native request associated with its immutable transport selection.</summary>
    internal static Task<IResult> AuthorizeAsync(HttpContext http)
    {
        if (http.RequestServices.GetRequiredService<ProtocolProfiles>().Managed && ManagedAdmission.Find(
            http.Features.Get<OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreFeature>()!.Transaction!) is { } managed)
        {
            return ManagedDecision.AuthorizeAsync(http, managed);
        }
        var pilot = IsPilot(http, OpenIddictServerEndpointType.Authorization);
        return pilot ? AuthorizeEndpoint.HandleAsync(http, http.RequestServices.GetRequiredService<LoginInteractions>(),
            http.RequestServices.GetRequiredService<PilotEligibility>(), http.RequestServices.GetRequiredService<TimeProvider>()) :
            AdministratorDecision.AuthorizeAsync(http);
    }

    /// <summary>Keeps pilot eligibility and authorization-manager resolution out of administrator exchanges.</summary>
    internal static Task<IResult> ExchangeAsync(HttpContext http)
    {
        var pilot = IsPilot(http, OpenIddictServerEndpointType.Token);
        return pilot ? TokenEndpoint.HandleAsync(http, http.RequestServices.GetRequiredService<PilotEligibility>(),
            http.RequestServices.GetRequiredService<IOpenIddictAuthorizationManager>()) : AdministratorDecision.ExchangeAsync(http);
    }

    /// <summary>Rejects unknown/disabled profiles, arbitrary endpoint entry and any native request identity substitution.</summary>
    private static bool IsPilot(HttpContext http, OpenIddictServerEndpointType endpoint)
    {
        var host = http.RequestServices.GetRequiredService<ProtocolHostRequest>();
        var profiles = http.RequestServices.GetRequiredService<ProtocolProfiles>();
        var outer = host.Outer;
        if (outer is null || !outer.IsRequestSkipped || outer.IsRequestHandled || outer.IsRejected || outer.EndpointType != endpoint ||
            !ReferenceEquals(outer.Transaction.Request, http.GetOpenIddictServerRequest()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        host.RequireInferred(outer);
        if (host.IsPilot && profiles.Pilot && outer.Transaction.Request?.ClientId == PilotProfile.ClientId)
        {
            return true;
        }
        return !host.IsPilot && profiles.Administrator is not null && outer.Transaction.Request?.ClientId == AdministratorProtocolIdentity.ClientId &&
            ProtocolStorageAmbient.Current is not null ? false : throw ProtocolStorageOwner.Unavailable();
    }
}
