using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Contains data only after child cleanup; it is not an issuance or native-error authorization permit.</summary>
internal sealed class ProtocolResponseOutput
{
    private readonly HttpContext context;
    private readonly ProtocolStorageAmbient parent;
    private readonly ProtocolResponseCapture destination;
    private readonly int status;
    private readonly string? reason;
    private readonly KeyValuePair<string, string?[]>[] headers;
    private readonly byte[] body;
    private int copied;

    /// <summary>Copies bounded frozen data without retaining callbacks, child services or mutable native response objects.</summary>
    internal ProtocolResponseOutput(HttpContext context, ProtocolStorageAmbient parent, ProtocolResponseCapture destination, ProtocolResponseCapture source)
    {
        this.context = context;
        this.parent = parent;
        this.destination = destination;
        status = source.StatusCode;
        reason = source.ReasonPhrase;
        headers = source.Headers.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.ToArray())).ToArray();
        body = source.Bytes.ToArray();
    }

    /// <summary>Copies once into the original still-unstarted parent capture; the protocol coordinator must separately authorize final publication.</summary>
    internal async Task CopyToParentAsync(CancellationToken token)
    {
        parent.Request.RequireCurrent();
        if (Interlocked.Exchange(ref copied, 1) != 0 || !ReferenceEquals(ProtocolStorageAmbient.Current?.Request, parent.Request) ||
            !ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, parent.Scope) || !ReferenceEquals(context.Features.Get<IHttpResponseFeature>(), destination) ||
            !ReferenceEquals(context.Features.Get<IHttpResponseBodyFeature>(), destination) || destination.HasStarted)
        {
            parent.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        destination.StatusCode = status;
        destination.ReasonPhrase = reason;
        destination.Headers.Clear();
        foreach (var header in headers)
        {
            destination.Headers[header.Key] = new StringValues((string?[])header.Value.Clone());
        }
        await destination.Stream.WriteAsync(body, token);
        parent.Request.RequireCurrent();
    }

    /// <summary>Seals the parent immediately and rejects callback or producer mutation of the exact post-cleanup child output.</summary>
    internal async Task CopyAndSealParentAsync(CancellationToken token)
    {
        await CopyToParentAsync(token);
        await destination.SealAsync();
        RequireFrozenParent();
    }

    /// <summary>Checks immutable output and original request/scope identity; no disposed child service is needed for publication admission.</summary>
    internal void RequireFrozenParent()
    {
        RequireFrozenCapture(parent.Request, destination);
        if (Volatile.Read(ref copied) != 1 || !ReferenceEquals(ProtocolStorageAmbient.Current?.Request, parent.Request) ||
            !ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, parent.Scope) ||
            !ReferenceEquals(context.Features.Get<IHttpResponseFeature>(), destination) || !ReferenceEquals(context.Features.Get<IHttpResponseBodyFeature>(), destination) ||
            !destination.IsSealed)
        {
            parent.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }
    /// <summary>Checks sealed data at final HTTP gates after parent-scope disposal without requiring disposed ambient services or restored feature identity.</summary>
    internal void RequireFrozenCapture(ProtocolStorageRequest request, ProtocolResponseCapture response)
    {
        request.RequireCurrent();
        if (!ReferenceEquals(parent.Request, request) || !ReferenceEquals(destination, response) || !response.BelongsTo(request) ||
            Volatile.Read(ref copied) != 1 || !response.IsSealed || response.StatusCode != status || response.ReasonPhrase != reason ||
            !response.Bytes.Span.SequenceEqual(body) || response.Headers.Count != headers.Length ||
            headers.Any(pair => !response.Headers.TryGetValue(pair.Key, out var actual) || !actual.ToArray().SequenceEqual(pair.Value, StringComparer.Ordinal)))
        {
            request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

}
