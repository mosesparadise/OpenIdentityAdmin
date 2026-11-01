using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Freezes the actual original endpoint scope and empty capture around native passthrough, independently of later child output.</summary>
internal sealed class ManagedEndpointParent
{
    private readonly HttpContext http;
    private readonly ProtocolStorageRequest request;
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolResponseCapture capture;
    private readonly string headers;

    /// <summary>Records only the live root endpoint parent before the installed native passthrough executes.</summary>
    internal ManagedEndpointParent(HttpContext http)
    {
        this.http = http;
        var current = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        request = current.Request;
        scope = current.Scope;
        capture = http.Features.Get<IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
        headers = Headers();
        RequireEmpty();
    }

    /// <summary>Rejects same-request foreign scopes, services, response bodies and captures through final transport publication.</summary>
    internal bool Current() => ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) &&
        ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) && ReferenceEquals(http.RequestServices, scope.Services) &&
        ReferenceEquals(http.Features.Get<IHttpResponseFeature>(), capture) && ReferenceEquals(http.Features.Get<IHttpResponseBodyFeature>(), capture) &&
        capture.BelongsTo(request);

    /// <summary>Requires unchanged status/headers and no staged output at every pre-sign-in handoff boundary.</summary>
    internal void RequireEmpty()
    {
        request.RequireCurrent();
        if (!Current() || capture.HasStarted || !capture.Bytes.IsEmpty || headers != Headers())
        {
            request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Freezes response metadata without including cookies or headers in diagnostic text.</summary>
    private string Headers() => FactorBinding.Digest(new
    {
        capture.StatusCode,
        capture.ReasonPhrase,
        Headers = capture.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray()
    });
}
