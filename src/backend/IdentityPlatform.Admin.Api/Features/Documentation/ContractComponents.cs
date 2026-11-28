using System.Text.Json.Nodes;

namespace IdentityPlatform.Admin.Api.Features.Documentation;

/// <summary>Retains only transitive local schema references and named security schemes used by visible operations.</summary>
internal static class ContractComponents
{
    private const string Schemas = "schemas";
    private const string Properties = "properties";
    private const string Schemes = "securitySchemes";
    private const string Reference = "$ref";
    private const string SchemaPrefix = "#/components/schemas/";
    private const string ExtensionPrefix = "x-";
    private static readonly string[] LiteralValues = ["example", "default", "const", "enum"];
    private static readonly string[] Unsupported = ["$dynamicRef", "$recursiveRef", "operationRef", "operationIdRef", "callbacks", "links",
        "discriminator", "externalDocs", "$id", "$anchor", "$dynamicAnchor", "$schema"];

    /// <summary>Uses a visited set for shared or recursive schemas and rejects unsupported reference forms before returning a document.</summary>
    internal static JsonObject Reachable(JsonObject source, JsonObject document)
    {
        var kept = new JsonObject();
        var pending = new Queue<(string Kind, string Name)>();
        var visited = new HashSet<(string Kind, string Name)>();
        Scan(document[PermissionContract.Paths], pending);
        Security(document[PermissionContract.Security], pending);
        while (pending.TryDequeue(out var reference))
        {
            if (!visited.Add(reference))
            {
                continue;
            }
            var value = source[PermissionContract.Components]?[reference.Kind]?[reference.Name] ?? throw new InvalidDataException();
            if (kept[reference.Kind] is not JsonObject bucket)
            {
                bucket = new JsonObject();
                kept.Add(reference.Kind, bucket);
            }
            bucket.Add(reference.Name, value.DeepClone());
            Scan(value, pending);
        }
        return kept;
    }

    /// <summary>Traverses the finite native JSON tree and enqueues referenced component roots without following remote URLs.</summary>
    private static void Scan(JsonNode? node, Queue<(string Kind, string Name)> pending)
    {
        if (node is JsonArray array)
        {
            foreach (var value in array)
            {
                Scan(value, pending);
            }
        }
        if (node is not JsonObject obj)
        {
            return;
        }
        foreach (var (name, value) in obj)
        {
            if (LiteralValues.Contains(name, StringComparer.Ordinal) || name.StartsWith(ExtensionPrefix, StringComparison.Ordinal))
            {
                continue;
            }
            if (Unsupported.Contains(name, StringComparer.Ordinal))
            {
                throw new InvalidDataException();
            }
            if (name == Properties && value is JsonObject properties)
            {
                foreach (var property in properties)
                {
                    Scan(property.Value, pending);
                }
            }
            else if (name == Reference)
            {
                var reference = value?.GetValue<string>() ?? throw new InvalidDataException();
                if (!reference.StartsWith(SchemaPrefix, StringComparison.Ordinal) || !LocalName(reference[SchemaPrefix.Length..]))
                {
                    throw new InvalidDataException();
                }
                pending.Enqueue((Schemas, reference[SchemaPrefix.Length..]));
            }
            else if (name == PermissionContract.Security)
            {
                Security(value, pending);
            }
            else
            {
                Scan(value, pending);
            }
        }
    }

    /// <summary>Accounts for security schemes referenced by name rather than by a JSON pointer.</summary>
    private static void Security(JsonNode? node, Queue<(string Kind, string Name)> pending)
    {
        if (node is null)
        {
            return;
        }
        foreach (var requirement in node.AsArray())
        {
            foreach (var (name, scopes) in requirement?.AsObject() ?? throw new InvalidDataException())
            {
                if (!LocalName(name) || scopes is not JsonArray)
                {
                    throw new InvalidDataException();
                }
                pending.Enqueue((Schemes, name));
            }
        }
    }

    /// <summary>Accepts the native generator's simple component identifiers without pointer escapes or nested fragments.</summary>
    private static bool LocalName(string value) => value.Length > 0 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
