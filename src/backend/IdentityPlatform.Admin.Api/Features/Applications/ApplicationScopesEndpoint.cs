using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes an additive current-version assignment resource without extending the existing editor DTO or issuer profile.</summary>
internal static class ApplicationScopesEndpoint
{
    private const string Separator = "/";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    private const string CurrentSegment = ".";
    private const string ParentSegment = "..";

    /// <summary>Admits an exact bodyless request before independently reading current application authority and native permissions.</summary>
    internal static Task ReadAsync(HttpContext http, string id) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        if (!ApplicationHttpBoundary.Exact(http, Path(id)) || !HttpMethods.IsGet(http.Request.Method) ||
            http.Request.ContentLength is not (null or 0) || http.Request.Headers.ContainsKey(TransferEncoding) || http.Request.Headers.ContainsKey(ContentEncoding) ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || await http.Request.Body.ReadAsync(new byte[1], http.RequestAborted) != 0)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationDetailsOperation.ReadOwnedAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock,
            (database, expiry, token) => ApplicationScopesReader.ReadAsync(database, id, expiry, token), budget.Token);
        await ApplicationHttpBoundary.PublishAsync(http, result.Scopes, StatusCodes.Status200OK, result.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && result.Scopes.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Uses one immutable assignment command and the existing owned mutation coordinator through commit and actual cleanup.</summary>
    internal static Task WriteAsync(HttpContext http, string id) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await InputAsync(http, id);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationMutationOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, command,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        if (result.Status != StatusCodes.Status200OK || result.Scopes is not { } output)
        {
            throw new ApplicationHttpRejected(result.Status);
        }
        await ApplicationHttpBoundary.PublishAsync(http, output, result.Status, result.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && output.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Separates missing preconditions from malformed tags and strict finite assignment JSON before native authentication or storage.</summary>
    private static async Task<ApplicationMutationCommand> InputAsync(HttpContext http, string id)
    {
        try
        {
            var (bytes, version) = await ApplicationMutationInput.ReadAsync(http, Path(id), HttpMethods.Put, true,
                malformedStatus: StatusCodes.Status400BadRequest);
            return ApplicationMutationCommand.AssignScopes(id, version!, ApplicationJson.Parse<ApplicationScopeInput>(bytes)) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Rejects unsupported identifiers before encoding one exact resource segment; suffix and major version are code-owned.</summary>
    private static string Path(string id) => ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) && id is not (CurrentSegment or ParentSegment)
        ? ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(id) + ApplicationScopeRules.Suffix
        : throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
}
