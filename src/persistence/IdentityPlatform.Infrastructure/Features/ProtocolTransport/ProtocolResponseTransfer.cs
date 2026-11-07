using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Copies a finite captured response only through the caller's publication guard; it grants no storage authority.</summary>
internal static class ProtocolResponseTransfer
{
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private const string NoStore = "no-store";

    /// <summary>Retains the caller's original admission checks around physical output and completion.</summary>
    internal static async Task PublishAsync(IHttpResponseFeature response, IHttpResponseBodyFeature body,
        ProtocolResponseCapture capture, Action require, CancellationToken token)
    {
        require();
        response.StatusCode = capture.StatusCode;
        response.ReasonPhrase = capture.ReasonPhrase;
        response.Headers.Clear();
        foreach (var header in capture.Headers)
        {
            response.Headers[header.Key] = header.Value;
        }
        await body.StartAsync(token);
        require();
        await body.Stream.WriteAsync(capture.Bytes, token);
        await body.Stream.FlushAsync(token);
        await body.CompleteAsync();
        require();
    }

    /// <summary>Discards every staged header and byte when the caller cannot admit the response.</summary>
    internal static async Task RejectAsync(IHttpResponseFeature response, IHttpResponseBodyFeature body, CancellationToken token)
    {
        response.Headers.Clear();
        response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        response.ReasonPhrase = null;
        response.Headers.CacheControl = NoStore;
        response.Headers[ReferrerPolicy] = NoReferrer;
        await body.StartAsync(token);
        await body.Stream.FlushAsync(token);
        await body.CompleteAsync();
    }
}
