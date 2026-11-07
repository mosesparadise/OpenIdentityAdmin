using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Adapts the established management budget/authentication/publication mechanisms to finite catalogue errors and read framing.</summary>
internal static class CatalogueHttp
{
    private const string Invalid = "Invalid catalogue request.";
    private const string SignIn = "Sign in required.";
    private const string Forbidden = "Catalogue access is forbidden.";
    private const string Missing = "Catalogue entry or deletion job not found.";
    private const string Conflict = "The catalogue entry conflicts with current state or is referenced.";
    private const string Precondition = "Reload the catalogue entry before submitting a new change.";
    private const string Unavailable = "The catalogue result is unavailable. Check its status before submitting again.";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Sanitizes domain failures before passing them through the shared original-budget HTTP boundary.</summary>
    internal static Task RunAsync(HttpContext http, Func<IdentityPlatform.Core.Features.Applications.ApplicationReadBudget, TimeProvider, Task> operation) =>
        ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
        {
            try
            {
                await operation(budget, clock);
            }
            catch (CatalogueRejected rejected) { throw new ApplicationHttpRejected(CatalogueMutationOperation.Status(rejected.Failure)); }
        }, Title);

    /// <summary>Returns fixed recovery text without reflecting submitted names, native errors or approval material.</summary>
    private static string Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => Invalid,
        StatusCodes.Status401Unauthorized => SignIn,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => Missing,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status412PreconditionFailed or StatusCodes.Status428PreconditionRequired => Precondition,
        _ => Unavailable
    };

    /// <summary>Requires exact HTTPS GET framing and actual absence of body bytes, retaining query only for collections.</summary>
    internal static async Task RequireReadAsync(HttpContext http, string path, bool collection)
    {
        var request = http.Request;
        var target = path + (collection ? request.QueryString.Value : string.Empty);
        if (!request.IsHttps || !HttpMethods.IsGet(request.Method) || request.Path.ToUriComponent() != path ||
            !collection && request.QueryString.HasValue || request.ContentLength is not (null or 0) ||
            request.Headers.ContainsKey(TransferEncoding) || request.Headers.ContainsKey(ContentEncoding) ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
            http.Features.Get<IHttpRequestFeature>()?.RawTarget is { Length: > 0 } raw && raw != target)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        try
        {
            await IdentityPlatform.Core.Features.Applications.ApplicationJson.ReadAsync(request.Body, 0, http.RequestAborted);
        }
        catch (InvalidDataException) { throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest); }
    }

    /// <summary>Maps only route-owned finite resource values to the shared public path vocabulary.</summary>
    internal static string Kind(CatalogueResource resource) => resource switch
    {
        CatalogueResource.Scope => CatalogueVersion.Scope,
        CatalogueResource.Audience => CatalogueVersion.Audience,
        _ => throw new InvalidOperationException()
    };
}
