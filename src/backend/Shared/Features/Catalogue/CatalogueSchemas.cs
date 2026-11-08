using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Catalogue;
using Microsoft.OpenApi;

namespace IdentityPlatform.Management.Features.Catalogue;

/// <summary>Publishes exact finite catalogue vocabulary and required closed DTO members from native schema generation.</summary>
internal static class CatalogueSchemas
{
    private const string Kind = "kind";
    private const string Disclosure = "disclosure";
    private const string State = "state";
    private const string Failure = "failure";
    private const string VersionField = "version";
    private const string KeyField = "requestKey";
    private const string Confirmed = "confirmed";
    private const string Id = "id";
    private const string TokenSchema = "CatalogueAntiforgeryToken";
    private const int GuidLength = 36;
    private const int VersionLength = 66;
    private const string GuidPattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
    private const string VersionPattern = "^\"[0-9a-f]{64}\"$";
    private static readonly string[] ScopeKinds = ["identity", "api"];
    private static readonly string[] Disclosures = ["none", "email"];
    private static readonly string[] ResourceKinds = ["scope", "audience"];
    private static readonly string[] States = ["queued", "succeeded", "rejected"];
    private static readonly string[] Failures = ["none", "stale", "referenced", "authority", "expired", "missing"];
    private static readonly string[] Names = [nameof(AudienceInput), nameof(ScopeInput), nameof(AudienceDetails), nameof(ScopeDetails),
        nameof(CatalogueDeleteInput), nameof(CatalogueDeletionDetails), nameof(CatalogueSummary), nameof(CataloguePage), TokenSchema];

    /// <summary>Closes only schemas used by implemented catalogue surfaces without adding absent production operations.</summary>
    internal static void Apply(OpenApiDocument document)
    {
        foreach (var name in Names)
        {
            if (document.Components?.Schemas?.TryGetValue(name, out var item) != true || item is not OpenApiSchema schema || schema.Properties is null)
            {
                continue;
            }
            schema.AdditionalPropertiesAllowed = false;
            schema.Required = schema.Properties.Keys.ToHashSet(StringComparer.Ordinal);
            if (name is nameof(ScopeInput) or nameof(ScopeDetails))
            {
                schema.Properties[Kind] = Vocabulary(ScopeKinds);
                schema.Properties[Disclosure] = Vocabulary(Disclosures);
            }
            if (name is nameof(ScopeDetails) or nameof(AudienceDetails))
            {
                schema.Properties[VersionField] = Version();
            }
            if (name == nameof(CatalogueDeleteInput))
            {
                schema.Properties[Confirmed] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)] };
                schema.Properties[KeyField] = Key();
            }
            if (name == nameof(CatalogueDeletionDetails))
            {
                schema.Properties[Id] = Key();
                schema.Properties[Kind] = Vocabulary(ResourceKinds);
                schema.Properties[State] = Vocabulary(States);
                schema.Properties[Failure] = Vocabulary(Failures);
            }
        }
    }

    /// <summary>Documents the catalogue's own opaque strong tag without confusing it with application aggregate version syntax.</summary>
    internal static OpenApiSchema Version() => new() { Type = JsonSchemaType.String, MinLength = VersionLength, MaxLength = VersionLength, Pattern = VersionPattern };

    /// <summary>Rejects alternate UUID spellings at the contract boundary; runtime independently rejects the all-zero UUID.</summary>
    internal static OpenApiSchema Key() => new() { Type = JsonSchemaType.String, MinLength = GuidLength, MaxLength = GuidLength, Pattern = GuidPattern };

    /// <summary>Exposes exact case-sensitive alternatives, never arbitrary claims or free-form internal failure messages.</summary>
    private static OpenApiSchema Vocabulary(string[] values) => new() { Type = JsonSchemaType.String, Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList() };
}
