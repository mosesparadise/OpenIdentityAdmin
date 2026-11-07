using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Publishes finite catalogue writes through the existing current-authority coordinators and original cleanup/publication budget.</summary>
internal static class CatalogueMutationEndpoint
{
    /// <summary>Parses immutable intent before native authentication and never automatically retries an ambiguous committed result.</summary>
    internal static Task HandleAsync(HttpContext http, CatalogueResource resource, string? id = null, bool delete = false) =>
        CatalogueHttp.RunAsync(http, async (budget, clock) =>
        {
            var command = await ReadAsync(http, resource, id, delete);
            var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
            var result = await ExecuteAsync(http, command, identity, budget, clock);
            http.Response.Headers.Location = result.Location;
            await ApplicationHttpBoundary.PublishAsync(http, result.Output, result.Status, result.Version, budget,
                () => identity.ExpiresAt > clock.GetUtcNow() && result.ExpiresAt > clock.GetUtcNow());
        });

    /// <summary>Shares framing and exact body limits while selecting the catalogue's independent strong-version grammar.</summary>
    private static async Task<object> ReadAsync(HttpContext http, CatalogueResource resource, string? id, bool delete)
    {
        if (id is not null && !CataloguePaths.IsTarget(id) || delete && id is null)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        var kind = CatalogueHttp.Kind(resource);
        var path = CataloguePaths.ApiRoot + (id is null ? CataloguePaths.Collection(kind) : CataloguePaths.Detail(kind, id));
        try
        {
            var input = await ApplicationMutationInput.ReadAsync(http, path, delete ? HttpMethods.Delete : id is null ? HttpMethods.Post : HttpMethods.Put,
                id is not null, CatalogueVersion.IsCanonical, StatusCodes.Status400BadRequest);
            if (delete)
            {
                return CatalogueDeleteIntent.Create(resource, id!, input.Version!, ApplicationJson.Parse<CatalogueDeleteInput>(input.Bytes)) ?? throw new InvalidDataException();
            }
            return Policy(input.Bytes, resource, id, input.Version) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Validates only the explicit resource DTO; no arbitrary native policy fields can enter a persisted intent.</summary>
    private static CatalogueIntent? Policy(byte[] bytes, CatalogueResource resource, string? id, string? version)
    {
        if (resource == CatalogueResource.Scope)
        {
            var scope = ApplicationJson.Parse<ScopeInput>(bytes);
            return id is null ? CatalogueIntent.Create(scope) : CatalogueIntent.Edit(id, version!, scope);
        }
        var audience = ApplicationJson.Parse<AudienceInput>(bytes);
        return id is null ? CatalogueIntent.Create(audience) : CatalogueIntent.Edit(id, version!, audience);
    }

    /// <summary>Projects only actual committed state and the private receipt's original expiry after native child disposal.</summary>
    private static async Task<Publication> ExecuteAsync(HttpContext http, object command, ApiIdentity identity, ApplicationReadBudget budget, TimeProvider clock)
    {
        var factory = http.RequestServices.GetRequiredService<IServiceScopeFactory>();
        var profile = http.RequestServices.GetRequiredService<ApiResourceProfile>();
        if (command is CatalogueDeleteIntent deletion)
        {
            var accepted = await CatalogueDeleteOperation.ExecuteAsync(factory, identity, deletion, profile, clock, budget, Guid.NewGuid());
            var job = accepted.Job ?? throw new ApplicationHttpRejected(accepted.Status);
            return new(job, accepted.Status, null, job.ExpiresAt, CataloguePaths.ApiRoot + CataloguePaths.Job(job.Kind, job.Id));
        }
        var intent = (CatalogueIntent)command;
        var result = await CatalogueMutationOperation.ExecuteAsync(factory, identity, intent, profile, clock, budget, Guid.NewGuid());
        var expiry = result.ExpiresAt ?? throw new ApplicationHttpRejected(result.Status);
        object output = result.Scope is { } scope ? scope.Details(result.Referenced, expiry) : result.Audience!.Details(result.Referenced, expiry);
        var id = result.Scope?.Id ?? result.Audience!.Id;
        var version = result.Scope?.Version ?? result.Audience!.Version;
        return new(output, result.Status, version, expiry, intent.Action == CatalogueAction.Create ? CataloguePaths.ApiRoot + CataloguePaths.Detail(CatalogueHttp.Kind(intent.Resource), id) : null);
    }

    /// <summary>Freezes one finite result and route-owned location; no arbitrary headers or native entities are published.</summary>
    private sealed record Publication(object Output, int Status, string? Version, DateTimeOffset ExpiresAt, string? Location);
}
