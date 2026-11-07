using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.ApplicationGrants;
using Microsoft.OpenApi;

namespace IdentityPlatform.Management.Features.ApplicationGrants;

/// <summary>Defines closed input and safe output vocabulary identically on both management surfaces.</summary>
internal static class GrantSchemas
{
    private const string Reason = "reason";
    private const string Confirm = "confirm";
    private const string State = "state";
    private const string Failure = "failure";
    private const string Id = "id";
    private const string RequestKey = "requestKey";
    private static readonly string[] Reasons = ["maintenance", "security", "configuration"];
    private static readonly string[] States = ["queued", "processing", "stopped", "succeeded"];
    private static readonly string[] Failures = ["none", "authority", "target", "storage", "clock"];

    /// <summary>Preserves native DTO generation while explicitly describing finite values and rejecting unknown members.</summary>
    internal static void Apply(OpenApiDocument document)
    {
        if (Schema(document, nameof(GrantRevocation)) is { } input)
        {
            input.Properties![Reason] = Vocabulary(Reasons);
            input.Properties[Confirm] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)] };
            input.Required = new HashSet<string> { Reason, Confirm };
            input.AdditionalPropertiesAllowed = false;
        }
        if (Schema(document, nameof(GrantJobView)) is { } job)
        {
            job.Properties![State] = Vocabulary(States);
            job.Properties[Failure] = Vocabulary(Failures);
            job.Properties[Id] = GrantContract.Key();
            job.AdditionalPropertiesAllowed = false;
        }
        if (Schema(document, nameof(GrantAcceptance)) is { } accepted)
        {
            accepted.Properties![RequestKey] = GrantContract.Key();
            accepted.AdditionalPropertiesAllowed = false;
        }
        if (Schema(document, nameof(GrantJobPage)) is { } page)
        {
            page.AdditionalPropertiesAllowed = false;
        }
    }

    /// <summary>Locates a native generated schema without introducing absent operations into dormant documents.</summary>
    private static OpenApiSchema? Schema(OpenApiDocument document, string name) =>
        document.Components?.Schemas?.TryGetValue(name, out var schema) == true ? schema as OpenApiSchema : null;

    /// <summary>Publishes exact case-sensitive wire alternatives with no arbitrary free-form text.</summary>
    private static OpenApiSchema Vocabulary(string[] values) => new()
    {
        Type = JsonSchemaType.String,
        Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList()
    };
}
