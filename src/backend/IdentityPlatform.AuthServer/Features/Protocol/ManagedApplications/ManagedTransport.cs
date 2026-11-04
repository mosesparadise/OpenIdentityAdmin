using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Admits a separate closed raw authorization shape without relaxing fixed administrator transport rules.</summary>
internal static class ManagedTransport
{
    private const int MaximumQueryLength = 16384;
    private const int MaximumFieldLength = 4096;

    /// <summary>Uses only canonical scalar query identity to select a candidate, never registration or issuance authority.</summary>
    internal static bool Candidate(OpenIddictServerEvents.ProcessRequestContext context) =>
        context.Transaction.GetHttpRequest() is { } request && context.EndpointType == OpenIddictServerEndpointType.Authorization &&
        request.Query.TryGetValue(OpenIddictConstants.Parameters.ClientId, out var client) && client.Count == 1 && ManagedDemand.Candidate(client[0]);

    /// <summary>Requires exact HTTPS GET framing and bounded known fields; native validation still owns callbacks, PKCE and cached restoration.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        var request = context.Transaction.GetHttpRequest()!;
        if (!request.IsHttps || !HttpMethods.IsGet(request.Method) || request.Path != ProtocolOptions.AuthorizePath ||
            request.QueryString.Value is not { Length: <= MaximumQueryLength } || request.Headers.Authorization.Count != 0 ||
            request.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget != request.Path + request.QueryString ||
            request.ContentLength is not (null or 0) || request.Headers.TransferEncoding.Count != 0 || request.Headers.ContentEncoding.Count != 0 ||
            request.Query.Any(pair => !Field(pair)) || request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
            await request.Body.ReadAsync(new byte[1], context.CancellationToken) != 0)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }

    /// <summary>Rejects duplicates, empty-present values, case aliases and extensions before native normalization can erase them.</summary>
    private static bool Field(KeyValuePair<string, StringValues> pair) => Allowed(pair.Key) && pair.Value.Count == 1 &&
        pair.Value[0] is { Length: > 0 and <= MaximumFieldLength };

    /// <summary>Names the entire supported initial/restored authorization parameter surface.</summary>
    internal static bool Allowed(string name) => name is OpenIddictConstants.Parameters.ClientId or OpenIddictConstants.Parameters.RedirectUri or
        OpenIddictConstants.Parameters.ResponseType or OpenIddictConstants.Parameters.ResponseMode or OpenIddictConstants.Parameters.Scope or
        OpenIddictConstants.Parameters.CodeChallenge or OpenIddictConstants.Parameters.CodeChallengeMethod or OpenIddictConstants.Parameters.State or
        OpenIddictConstants.Parameters.Nonce or OpenIddictConstants.Parameters.Prompt or OpenIddictConstants.Parameters.RequestUri;
}
