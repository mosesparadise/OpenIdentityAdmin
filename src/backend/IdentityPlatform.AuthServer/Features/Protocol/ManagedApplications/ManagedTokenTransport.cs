using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Bounds the sole managed backchannel profile before native Basic authentication and code validation.</summary>
internal static class ManagedTokenTransport
{
    private const int MaximumHeaderLength = 4096;
    private const int MaximumFieldLength = 4096;
    private const int FieldCount = 4;
    private const string Basic = "Basic ";
    private const string FormType = "application/x-www-form-urlencoded";

    /// <summary>Selects only a token endpoint carrying Basic framing; the library still authenticates the decoded client and secret.</summary>
    internal static bool Candidate(OpenIddictServerEvents.ProcessRequestContext context) =>
        context.EndpointType == OpenIddictServerEndpointType.Token && context.Transaction.GetHttpRequest() is { } request &&
        request.Headers.Authorization.Count == 1 && request.Headers.Authorization[0] is { } value && value.StartsWith(Basic, StringComparison.Ordinal);

    /// <summary>Rejects aliases, duplicates, body client credentials, extensions and ambiguous raw framing before normalization.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        var request = context.Transaction.GetHttpRequest()!;
        if (!request.IsHttps || !HttpMethods.IsPost(request.Method) || request.Path != ProtocolOptions.TokenPath ||
            request.PathBase.HasValue || request.QueryString.HasValue ||
            request.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget != ProtocolOptions.TokenPath ||
            request.Headers.Authorization[0] is not { Length: > 0 and <= MaximumHeaderLength } ||
            request.Headers.ContentEncoding.Count != 0 || !request.HasFormContentType ||
            !MediaTypeHeaderValue.TryParse(request.ContentType, out var media) || !media.MediaType.Equals(FormType, StringComparison.OrdinalIgnoreCase))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        var fields = await request.ReadFormAsync(context.CancellationToken);
        if (fields.Count != FieldCount || fields.Files.Count != 0 || fields.Any(pair => !Allowed(pair.Key) ||
            pair.Value.Count != 1 || pair.Value[0] is not { Length: > 0 and <= MaximumFieldLength }) ||
            fields[OpenIddictConstants.Parameters.GrantType] != OpenIddictConstants.GrantTypes.AuthorizationCode)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }

    /// <summary>Enumerates the exact native code exchange fields, excluding all alternate authentication or grant modes.</summary>
    private static bool Allowed(string field) => field is OpenIddictConstants.Parameters.GrantType or OpenIddictConstants.Parameters.Code or
        OpenIddictConstants.Parameters.RedirectUri or OpenIddictConstants.Parameters.CodeVerifier;
}
