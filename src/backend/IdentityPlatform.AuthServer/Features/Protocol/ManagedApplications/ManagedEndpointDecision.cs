using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Binds one real native handoff to a fresh managed principal and one sign-in continuation.</summary>
internal sealed class ManagedEndpointDecision
{
    private static readonly object Key = new();
    private readonly OpenIddictServerEvents.ProcessRequestContext outer;
    private readonly ClaimsPrincipal principal;
    private readonly string principalBinding;
    private readonly string parameters;
    private readonly ProtocolStorageRequest request;
    private readonly ManagedEndpointParent parent;
    private OpenIddictServerEvents.ProcessSignInContext? signIn;

    /// <summary>Captures endpoint intent only after actual native passthrough has completed on the original request.</summary>
    private ManagedEndpointDecision(HttpContext http, ClaimsPrincipal principal)
    {
        outer = http.RequestServices.GetRequiredService<ProtocolHostRequest>().Outer ?? throw ProtocolStorageOwner.Unavailable();
        this.principal = principal;
        principalBinding = ManagedConsumption.Claims(principal);
        parameters = ManagedConsumption.Parameters(outer.Transaction.Request!);
        request = ProtocolStorageAmbient.Current!.Request;
        parent = (outer.EndpointType == OpenIddictServerEndpointType.Authorization ? ManagedAdmission.Find(outer.Transaction)?.Parent :
            ManagedTokenAdmission.Find(outer.Transaction)?.Parent) ?? throw ProtocolStorageOwner.Unavailable();
        parent.RequireEmpty();
        if (!Current())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Returns actual native sign-in while retaining its private original principal and handoff receipt.</summary>
    internal static IResult Attach(HttpContext http, ClaimsPrincipal principal)
    {
        http.Items.Add(Key, new ManagedEndpointDecision(http, principal));
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Projects only identity-only managed code values; the later locked issuance epoch rereads all source authority.</summary>
    internal static async Task<IResult> ExchangeAsync(HttpContext http)
    {
        var authentication = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var original = authentication.Principal;
        if (!authentication.Succeeded || original is null || !ManagedTokenBinding.Candidate(original) ||
            !long.TryParse(original.GetClaim(ManagedDemand.RevisionClaim), out var revision) || revision <= 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var principal = ManagedPrincipal.Exchange(original);
        return Attach(http, principal);
    }

    /// <summary>Consumes one endpoint receipt before native redemption, rejecting transplanted or changed principals.</summary>
    internal static ManagedEndpointDecision Claim(HttpContext http, OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (!http.Items.Remove(Key, out var value) || value is not ManagedEndpointDecision decision || decision.signIn is not null ||
            !decision.Current() || !ReferenceEquals(context.Transaction, decision.outer.Transaction) ||
            !ReferenceEquals(context.Principal, decision.principal) || decision.principalBinding != ManagedConsumption.Claims(context.Principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        decision.parent.RequireEmpty();
        decision.signIn = context;
        return decision;
    }

    /// <summary>Requires the same original handoff after native sign-in, commit and actual scope cleanup.</summary>
    internal bool Allows(OpenIddictServerEvents.ProcessSignInContext context) => ReferenceEquals(signIn, context) && Current() &&
        context.IsRequestHandled && !context.IsRejected && !context.IsRequestSkipped && ReferenceEquals(context.Transaction, outer.Transaction);

    /// <summary>Rejects altered endpoint or native request outcomes and all parameter substitutions.</summary>
    private bool Current() => parent.Current() && ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && outer.IsRequestSkipped &&
        !outer.IsRequestHandled && !outer.IsRejected && parameters == ManagedConsumption.Parameters(outer.Transaction.Request!) &&
        (outer.EndpointType == OpenIddictServerEndpointType.Authorization ? ManagedAdmission.Find(outer.Transaction)?.HasHandoff == true :
            outer.EndpointType == OpenIddictServerEndpointType.Token && ManagedTokenAdmission.Find(outer.Transaction)?.Current() == true);
}
