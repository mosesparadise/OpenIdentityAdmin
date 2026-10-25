using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Proves actual native end-session formatting and freezes its exact state-only fixed return.</summary>
internal sealed class IssuerLogoutResponse(OpenIddictServerEvents.ProcessSignOutContext signOut, string destination, string state)
{
    private OpenIddictServerEvents.ApplyEndSessionResponseContext? completed;
    private OpenIddictResponse? response;
    private ProtocolResponseCapture? capture;
    private string? frozen;

    /// <summary>Observes the installed query serializer rather than trusting a substituted handled flag or redirect.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.ApplyEndSessionResponseContext context, Func<ValueTask> native)
    {
        if (completed is not null || context.IsRequestHandled || context.IsRequestSkipped ||
            !ReferenceEquals(context.Transaction, signOut.Transaction) || context.PostLogoutRedirectUri != destination || !Parameters(context.Response))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var http = context.Transaction.GetHttpRequest()!.HttpContext;
        capture = http.Features.Get<IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
        if (capture.HasStarted || !capture.Bytes.IsEmpty)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        response = context.Response;
        await native();
        var location = capture.Headers.Location.ToString();
        if (!context.IsRequestHandled || context.IsRequestSkipped || !ReferenceEquals(capture, http.Features.Get<IHttpResponseFeature>()) ||
            capture.StatusCode != StatusCodes.Status302Found || !capture.Bytes.IsEmpty ||
            !Uri.TryCreate(location, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Path) != destination || uri.Fragment.Length != 0 ||
            QueryHelpers.ParseQuery(uri.Query) is not { Count: 1 } query || query[OpenIddictConstants.Parameters.State].Count != 1 ||
            query[OpenIddictConstants.Parameters.State][0] != state || !Parameters(response))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        completed = context;
        frozen = Digest(capture);
    }

    /// <summary>Rejects callback mutation after native formatting, child cleanup and immediately before parent admission.</summary>
    internal void RequireCompleted(bool activeCapture = true)
    {
        if (completed is not { IsRequestHandled: true, IsRequestSkipped: false } || capture is null ||
            signOut is not { IsRequestHandled: true, IsRequestSkipped: false, IsRejected: false } ||
            !ReferenceEquals(response, signOut.Transaction.Response) || completed.PostLogoutRedirectUri != destination ||
            !Parameters(response!) || activeCapture && frozen != Digest(capture))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Allows only the original scalar state; native errors or arbitrary extra credential fields cannot enter a success URL.</summary>
    private bool Parameters(OpenIddictResponse value) => value.GetParameters().Count == 1 &&
        value.GetParameters().TryGetValue(OpenIddictConstants.Parameters.State, out var parameter) &&
        (JsonElement)parameter is { ValueKind: JsonValueKind.String } scalar && scalar.GetString() == state;

    /// <summary>Freezes all response headers, status and bytes without retaining printable private response values.</summary>
    private static string Digest(ProtocolResponseCapture value) => FactorBinding.Digest(new
    {
        value.StatusCode,
        value.ReasonPhrase,
        Headers = value.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray(),
        Body = Convert.ToHexString(SHA256.HashData(value.Bytes.Span))
    });
}
