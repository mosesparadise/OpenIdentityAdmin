using System.Text;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises the real pilot publication guard with actual native context transitions and bounded response staging, without a database.</summary>
internal sealed class PilotServerErrorProbe : IDisposable
{
    internal const string Body = "{\"error\":\"server_error\"}";
    internal const string ContentType = "application/json;charset=UTF-8";
    internal ProtocolResponseCapture Capture
    {
        get;
    }
    internal OpenIddictResponse Response { get; } = new() { Error = OpenIddictConstants.Errors.ServerError };
    internal PilotExchange Exchange
    {
        get;
    }
    private readonly OpenIddictServerTransaction transaction;
    private readonly OpenIddictServerEvents.ProcessRequestContext outer;

    /// <summary>Retains one original HTTP/native transaction and a fresh monotonic request lifetime.</summary>
    internal PilotServerErrorProbe()
    {
        var http = new DefaultHttpContext();
        var request = new ProtocolStorageRequest(new ControlledProbeClock(), TestContext.Current.CancellationToken);
        Capture = new(request)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        Capture.Headers.ContentType = ContentType;
        transaction = new()
        {
            Options = new(),
            EndpointType = OpenIddictServerEndpointType.Token,
            Response = Response,
            Request = new()
            {
                ClientId = PilotProfile.ClientId,
                GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode
            }
        };
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        Exchange = new(http, request, Capture);
        outer = new(transaction);
        Exchange.Before(outer);
        outer.SkipRequest();
    }

    /// <summary>Models independently selectable native generation, cleanup and handled-error receipts before sealing actual staged bytes.</summary>
    internal async Task StageAsync(string body = Body, bool generation = true, bool closed = true, bool recognized = true, bool sealedOutput = true)
    {
        var signIn = new OpenIddictServerEvents.ProcessSignInContext(transaction);
        if (generation)
        {
            Exchange.Begin(signIn);
        }
        if (closed)
        {
            Exchange.Complete(signIn, committed: false);
        }
        var error = new OpenIddictServerEvents.ProcessErrorContext(transaction);
        Exchange.Before(error);
        error.HandleRequest();
        await Capture.Stream.WriteAsync(Encoding.UTF8.GetBytes(body), TestContext.Current.CancellationToken);
        if (recognized)
        {
            Exchange.After(error);
        }
        if (sealedOutput)
        {
            await Capture.SealAsync();
        }
    }

    /// <summary>Releases the owned staged body after the guard has accepted or rejected the candidate.</summary>
    public void Dispose() => Capture.Dispose();
}
