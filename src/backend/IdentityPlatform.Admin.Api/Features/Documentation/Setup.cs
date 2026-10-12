namespace IdentityPlatform.Admin.Api.Features.Documentation;

// Deliberately not called by the production pipeline. Real access policy is a later prerequisite.
internal static class Setup
{
    internal static void MapDocumentation(this WebApplication app) => app.MapOpenApi().WithDocumentPerVersion();
}
