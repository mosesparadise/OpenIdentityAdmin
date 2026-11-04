using System.Security.Cryptography;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Observes actual native self-redirection, rejecting arbitrary handled flags, foreign locations and mutated staged output.</summary>
internal sealed class ManagedCacheResponse(OpenIddictServerEvents.ProcessSignInContext signIn)
{
    private OpenIddictServerEvents.ApplyAuthorizationResponseContext? completed;
    private ProtocolResponseCapture? capture;
    private string? output;
    private const int ResponseFields = 2;

    /// <summary>Records one real formatter after exact native response mapping and before any bytes leave owned response capture.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context, Func<ValueTask> native)
    {
        var http = context.Transaction.GetHttpRequest()!.HttpContext;
        var request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        if (completed is not null || context.IsRequestHandled || context.IsRequestSkipped || !ReferenceEquals(signIn.Transaction, context.Transaction) ||
            !Parameters() || http.Features.Get<IHttpResponseFeature>() is not ProtocolResponseCapture current ||
            !current.BelongsTo(request) || current.HasStarted || !current.Bytes.IsEmpty)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        if (!context.IsRequestHandled || context.IsRequestSkipped || !Parameters() ||
            !ReferenceEquals(http.Features.Get<IHttpResponseFeature>(), current) || !Location(current, context))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        completed = context;
        capture = current;
        output = Snapshot(current);
    }

    /// <summary>Requires only the native reference and exact client identity in the cache redirect response.</summary>
    private bool Parameters() => signIn.Response.GetParameters().Count == ResponseFields &&
        (string?)signIn.Response.GetParameter(OpenIddictConstants.Parameters.ClientId) == signIn.ClientId && signIn.Response.RequestUri == OpenIddictConstants.RequestUris.Prefixes.Generic + signIn.RequestToken &&
        !string.IsNullOrEmpty(signIn.RequestToken) && string.IsNullOrEmpty(signIn.Response.Error);

    /// <summary>Requires the original issuer authorization endpoint and exact scalar native output, with no credential-bearing body.</summary>
    private bool Location(ProtocolResponseCapture current, OpenIddictServerEvents.ApplyAuthorizationResponseContext context)
    {
        if (current.StatusCode != StatusCodes.Status302Found || !current.Bytes.IsEmpty || current.Headers.Location.Count != 1 ||
            !Uri.TryCreate(current.Headers.Location[0], UriKind.Absolute, out var location) ||
            location.GetLeftPart(UriPartial.Path) != new Uri(context.Options.Issuer!, ProtocolOptions.AuthorizePath).AbsoluteUri ||
            !string.IsNullOrEmpty(location.Fragment))
        {
            return false;
        }
        var query = QueryHelpers.ParseQuery(location.Query);
        return query.Count == ResponseFields && query.All(pair => pair.Value.Count == 1) &&
            query[OpenIddictConstants.Parameters.ClientId] == signIn.ClientId &&
            query[OpenIddictConstants.Parameters.RequestUri] == signIn.Response.RequestUri;
    }

    /// <summary>Detects callbacks, headers, body, status or native-field changes after final response sealing.</summary>
    internal bool Current() => completed is { IsRequestHandled: true, IsRequestSkipped: false } && capture is not null &&
        signIn.IsRequestHandled && !signIn.IsRequestSkipped && !signIn.IsRejected && Parameters() && output == Snapshot(capture) &&
        ReferenceEquals(signIn.Transaction.GetHttpRequest()!.HttpContext.Features.Get<IHttpResponseFeature>(), capture);

    /// <summary>Hashes staged response material without retaining printable browser proofs or native references.</summary>
    private static string Snapshot(ProtocolResponseCapture value) => FactorBinding.Digest(new
    {
        value.StatusCode,
        value.ReasonPhrase,
        Headers = value.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray(),
        Body = Convert.ToHexString(SHA256.HashData(value.Bytes.Span))
    });
}
