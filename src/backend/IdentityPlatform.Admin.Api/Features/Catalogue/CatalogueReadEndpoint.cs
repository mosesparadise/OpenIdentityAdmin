using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Publishes exact catalogue collections, details and retained job status using independently validated native bearer authority.</summary>
internal static class CatalogueReadEndpoint
{
    private const string GuidFormat = "D";

    /// <summary>Retains one original budget through native authentication, rollback-only reads, cleanup and final response serialization.</summary>
    internal static Task HandleAsync(HttpContext http, CatalogueResource resource, string? id = null, string? jobId = null) =>
        CatalogueHttp.RunAsync(http, async (budget, clock) =>
        {
            var kind = CatalogueHttp.Kind(resource);
            var job = ReadJob(jobId);
            if (id is not null && !CataloguePaths.IsTarget(id))
            {
                throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
            }
            var collection = id is null && job is null;
            var path = CataloguePaths.ApiRoot + (job is { } value ? CataloguePaths.Job(kind, value) : id is not null ? CataloguePaths.Detail(kind, id) : CataloguePaths.Collection(kind));
            await CatalogueHttp.RequireReadAsync(http, path, collection);
            var query = collection ? Query(http) : null;
            var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
            var result = await CatalogueReadOperation.ReadAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity,
                resource, id, job, query, http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget.Token);
            await ApplicationHttpBoundary.PublishAsync(http, result.Output, StatusCodes.Status200OK, result.Version, budget,
                () => identity.ExpiresAt > clock.GetUtcNow() && result.ExpiresAt > clock.GetUtcNow());
        });

    /// <summary>Rejects aliases and empty durable identifiers before any current-authority observation.</summary>
    private static Guid? ReadJob(string? text) => text is null ? null : Guid.TryParseExact(text, GuidFormat, out var value) &&
        value != Guid.Empty && value.ToString(GuidFormat) == text ? value : throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);

    /// <summary>Uses the established finite management paging grammar without accepting duplicate or unknown query members.</summary>
    private static ApplicationListQuery Query(HttpContext http) => http.Request.QueryString.Value?.Length > ApplicationProjection.MaximumQueryCharacters
        ? throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest)
        : ApplicationListQuery.Parse(http.Request.Query.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value.ToArray()!)))
            ?? throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
}
