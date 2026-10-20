using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Binds the explicitly routed transport to one HTTP request and its actual outer native transaction.</summary>
internal sealed class ProtocolHostRequest
{
    private HttpContext? http;
    private OpenIddictServerEndpointType endpoint;
    private ProtocolHostMode mode;
    private bool extracted;
    internal OpenIddictServerEvents.ProcessRequestContext? Outer
    {
        get; private set;
    }
    internal bool IsPilot => mode == ProtocolHostMode.PilotProtocol;

    /// <summary>Allows one middleware routing decision before native handlers or account-page managers resolve.</summary>
    internal void Bind(HttpContext context, OpenIddictServerEndpointType expected, ProtocolHostMode selected)
    {
        if (http is not null || Outer is not null || selected == ProtocolHostMode.Unbound)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        http = context;
        endpoint = expected;
        mode = selected;
    }

    /// <summary>Captures the real outer native context; account/unknown fallbacks receive no protocol execution permission.</summary>
    internal void Track(OpenIddictServerEvents.ProcessRequestContext context)
    {
        if (Outer is not null || !ReferenceEquals(context.Transaction.GetHttpRequest()?.HttpContext, http))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        Outer = context;
    }

    /// <summary>Runs immediately after actual native inference, before extraction can resolve any manager.</summary>
    internal void RequireInferred(OpenIddictServerEvents.ProcessRequestContext context)
    {
        if (!ReferenceEquals(Outer, context) || context.EndpointType != endpoint ||
            (mode is ProtocolHostMode.Unknown or ProtocolHostMode.PilotAccount && endpoint != OpenIddictServerEndpointType.Unknown) ||
            (mode == ProtocolHostMode.Owned && ProtocolStorageAmbient.Current is null) || mode == ProtocolHostMode.Unbound)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Checks actual extracted identity before any native manager resolution and freezes the pilot profile.</summary>
    internal void RequirePilotExtraction(OpenIddictServerEvents.ProcessRequestContext context)
    {
        RequireInferred(context);
        if (!IsPilot || extracted || context.Transaction.Request?.ClientId != PilotProfile.ClientId)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        extracted = true;
    }

    /// <summary>Allows unowned nested native work only on the original explicit pilot transaction and extracted profile.</summary>
    internal void RequireUnowned(OpenIddictServerEvents.BaseContext context)
    {
        if (ProtocolStorageAmbient.Current is not null || mode is ProtocolHostMode.Owned or ProtocolHostMode.Unbound ||
            !ReferenceEquals(context.Transaction.GetHttpRequest()?.HttpContext, http))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (context is OpenIddictServerEvents.ProcessRequestContext outer)
        {
            Track(outer);
            return;
        }
        if (!IsPilot || Outer is null || !ReferenceEquals(Outer.Transaction, context.Transaction) || context.EndpointType != endpoint ||
            (extracted && context.Transaction.Request?.ClientId != PilotProfile.ClientId) ||
            (!extracted && context is not (OpenIddictServerEvents.ExtractAuthorizationRequestContext or OpenIddictServerEvents.ExtractTokenRequestContext) && !Outer.IsRejected))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}

/// <summary>Separates account manager access from protocol dispatch and from owned administrator publication.</summary>
internal enum ProtocolHostMode
{
    Unbound, Unknown, PilotAccount, PilotProtocol, Owned
}

/// <summary>Marks only the two explicitly enabled pilot Razor selectors that require legacy native managers.</summary>
internal sealed class PilotAccountEndpoint;
