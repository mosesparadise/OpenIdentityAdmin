using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using IdentityPlatform.Management.Features.ApplicationGrants;

namespace IdentityPlatform.Admin.Api.Features.ApplicationGrants;

/// <summary>Applies the shared grant contract to this host's explicit management surface.</summary>
internal sealed class GrantContractTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Describes only mapped operations, preserving the dormant production inventory.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        GrantContract.Apply(document, browser: false);
        return Task.CompletedTask;
    }
}
