using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Admits only canonical HTTPS GET bearer headers or POST header/form transport, with no query-token fallback.</summary>
internal static class UserInfoTransport
{
    private const int MaximumTokenLength = 16_384;
    private const string Bearer = "Bearer ";
    private const string FormType = "application/x-www-form-urlencoded";

    /// <summary>Rejects path aliases, query strings, duplicate or ambiguous credentials before native extraction can normalize them.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ExtractUserInfoRequestContext context)
    {
        var request = context.Transaction.GetHttpRequest()!;
        if (!request.IsHttps || request.Path.Value != ProtocolOptions.UserInfoPath || request.PathBase.HasValue || request.QueryString.HasValue ||
            request.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget != ProtocolOptions.UserInfoPath || request.Headers.ContentEncoding.Count != 0 ||
            request.Headers.Authorization.Count > 1 || request.Headers.Authorization.Count == 1 && !Header(request.Headers.Authorization[0]))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        if (HttpMethods.IsGet(request.Method))
        {
            if (request.ContentLength is > 0 || request.Headers.TransferEncoding.Count != 0 ||
                request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true)
            {
                context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            }
            return;
        }
        if (!HttpMethods.IsPost(request.Method) || !request.HasFormContentType ||
            !MediaTypeHeaderValue.TryParse(request.ContentType, out var media) || !media.MediaType.Equals(FormType, StringComparison.OrdinalIgnoreCase))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        var fields = await request.ReadFormAsync(context.CancellationToken);
        if (fields.Files.Count != 0 || fields.Count > 1 || fields.Count != 0 && (request.Headers.Authorization.Count != 0 ||
            !fields.TryGetValue(OpenIddictConstants.Parameters.AccessToken, out var values) || values.Count != 1 || !Token(values[0])))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }

    /// <summary>Allows only one bounded RFC bearer credential; native cryptography still establishes its validity.</summary>
    private static bool Header(string? value) => value is not null && value.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase) && Token(value[Bearer.Length..]);

    /// <summary>Rejects empty, whitespace and comma-combined credentials before any native parsing.</summary>
    private static bool Token(string? value) => value is { Length: > 0 and <= MaximumTokenLength } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~' or '+' or '/' or '=');
}
