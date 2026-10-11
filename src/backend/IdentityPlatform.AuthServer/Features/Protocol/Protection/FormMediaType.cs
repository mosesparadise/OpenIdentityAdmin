using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

internal static class FormMediaType
{
    internal static ValueTask ValidateAsync<TContext>(TContext context) where TContext : BaseValidatingContext
    {
        var request = context.Transaction.GetHttpRequest()!;
        // OpenIddict's prefix check admits malformed suffixes that ReadFormAsync cannot parse.
        // Leave missing/other media types and valid form parameters to the native extractor.
        if (HttpMethods.IsPost(request.Method) &&
            request.ContentType?.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true &&
            !request.HasFormContentType)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest,
                "The specified Content-Type header is invalid.",
                "https://documentation.openiddict.com/errors/ID2082");
        }
        return ValueTask.CompletedTask;
    }
}
