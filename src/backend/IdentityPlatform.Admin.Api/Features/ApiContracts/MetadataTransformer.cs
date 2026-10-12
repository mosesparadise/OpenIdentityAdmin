using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Api.Features.ApiContracts;

/// <summary>Applies validated presentation metadata to the authoritative native document, including build-time export.</summary>
internal sealed class MetadataTransformer(IOptions<OpenApiMetadataOptions> options) : IOpenApiDocumentTransformer
{
    private const string SameOriginServer = "/";

    /// <summary>Resolve validated options even when generation does not start the host; preserve unrelated document metadata.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = options.Value;
        document.Info ??= new OpenApiInfo();
        document.Info.Title = metadata.Title;
        document.Info.Description = metadata.Description;
        document.Info.Version = Setup.SupportedMajor.ToString(CultureInfo.InvariantCulture);
        document.Servers = [new OpenApiServer { Url = SameOriginServer }];
        return Task.CompletedTask;
    }
}
