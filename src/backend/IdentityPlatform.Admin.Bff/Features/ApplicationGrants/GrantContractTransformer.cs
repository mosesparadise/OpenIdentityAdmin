using IdentityPlatform.Management.Features.ApplicationGrants;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.ApplicationGrants;

/// <summary>Applies the shared grant contract to this host's explicit management surface.</summary>
internal sealed class GrantContractTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Describes only mapped operations, preserving the dormant production inventory.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        GrantContract.Apply(document, browser: true);
        return Task.CompletedTask;
    }
}
