using IdentityPlatform.Management.Features.Catalogue;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Applies the shared finite catalogue wire contract to this host's actual mapped surface.</summary>
internal sealed class CatalogueContractTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Leaves dormant production documents empty while documenting explicitly composed catalogue operations.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        CatalogueContract.Apply(document, browser: false);
        return Task.CompletedTask;
    }
}
