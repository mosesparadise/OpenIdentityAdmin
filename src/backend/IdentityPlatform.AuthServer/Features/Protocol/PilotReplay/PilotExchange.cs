using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Retains the original pilot HTTP/native identities without entering the owned administrator storage graph.</summary>
internal sealed class PilotExchange(HttpContext http, ProtocolStorageRequest request, ProtocolResponseCapture capture)
{
    private static readonly AsyncLocal<PilotExchange?> Ambient = new();
    private OpenIddictServerEvents.ProcessRequestContext? outer;
    private OpenIddictServerEvents.ProcessSignInContext? success;
    private bool nativeError;
    private bool generationStarted;
    internal bool StorageClosed
    {
        get; private set;
    }
    internal static PilotExchange? Current => Ambient.Value;
    internal HttpContext Http => http;
    internal ProtocolStorageRequest Request => request;
    internal ProtocolResponseCapture Capture => capture;

    /// <summary>Installs one private request identity; disposal restores only the enclosing async flow.</summary>
    internal IDisposable Enter()
    {
        var previous = Ambient.Value;
        Ambient.Value = this;
        return new Restore(() => Ambient.Value = previous);
    }

    /// <summary>Tracks the actual outer native request, never an inferred or reconstructed context.</summary>
    internal void Before(OpenIddictServerEvents.BaseContext context)
    {
        request.RequireCurrent();
        if (!ReferenceEquals(context.Transaction.GetHttpRequest()?.HttpContext, http))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (context is OpenIddictServerEvents.ProcessRequestContext value)
        {
            if (outer is not null)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            outer = value;
        }
        else if (outer is null || !ReferenceEquals(outer.Transaction, context.Transaction))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Recognizes native error completion only on the original transaction after native response formatting returns.</summary>
    internal void After(OpenIddictServerEvents.BaseContext context)
    {
        request.RequireCurrent();
        if (context is OpenIddictServerEvents.ProcessRequestContext or OpenIddictServerEvents.ProcessErrorContext or OpenIddictServerEvents.ProcessChallengeContext &&
            context is OpenIddictServerEvents.BaseRequestContext { IsRequestHandled: true, IsRequestSkipped: false } &&
            !string.IsNullOrEmpty(context.Transaction.Response?.Error))
        {
            nativeError = true;
        }
    }

    /// <summary>Admits exactly one actual sign-in following native token passthrough on this pilot request.</summary>
    internal void Begin(OpenIddictServerEvents.ProcessSignInContext context)
    {
        Before(context);
        if (generationStarted || outer is not { IsRequestSkipped: true, IsRejected: false, IsRequestHandled: false } || !Candidate(context))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        generationStarted = true;
    }

    /// <summary>Closes all request storage after actual generation cleanup and retains only a successful native continuation.</summary>
    internal void Complete(OpenIddictServerEvents.ProcessSignInContext context, bool committed)
    {
        StorageClosed = true;
        request.RequireCurrent();
        if (committed && context is { IsRequestHandled: true, IsRejected: false, IsRequestSkipped: false })
        {
            success = context;
        }
    }

    /// <summary>Requires a private completed generation receipt or an actual native error before copying any staged byte.</summary>
    internal void RequirePublication()
    {
        request.RequireCurrent();
        if (!capture.IsSealed || (capture.StatusCode == StatusCodes.Status200OK
            ? success is not { IsRequestHandled: true, IsRejected: false, IsRequestSkipped: false }
            : !nativeError || capture.StatusCode is not (StatusCodes.Status400BadRequest or StatusCodes.Status401Unauthorized)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Restricts coordination to pilot authorization-code sign-in; native handling remains unchanged for all other contexts.</summary>
    internal static bool Candidate(OpenIddictServerEvents.ProcessSignInContext context) =>
        context.EndpointType == OpenIddictServerEndpointType.Token && context.Request.IsAuthorizationCodeGrantType() && context.ClientId == PilotProfile.ClientId;

    /// <summary>Restores only the ambient holder and never disposes native request services.</summary>
    private sealed class Restore(Action restore) : IDisposable
    {
        /// <summary>Restores the enclosing async flow after all actual request work has drained.</summary>
        public void Dispose() => restore();
    }
}
