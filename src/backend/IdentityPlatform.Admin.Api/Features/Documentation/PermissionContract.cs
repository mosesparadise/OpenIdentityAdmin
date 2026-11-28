using System.Text.Json.Nodes;

namespace IdentityPlatform.Admin.Api.Features.Documentation;

/// <summary>Projects one authoritative document for an exact current grant set without mutating the native contract.</summary>
internal static class PermissionContract
{
    internal const string Paths = "paths";
    internal const string Components = "components";
    internal const string Security = "security";
    internal const string Tags = "tags";
    private const string Required = "x-required-permission";
    private const string RequiredTogether = "x-required-permissions";
    private const string Parameters = "parameters";
    private const string TagName = "name";
    private static readonly string[] Methods = ["get", "post", "put", "patch", "delete", "options", "head", "trace"];
    private static readonly string[] Metadata = ["openapi", "info", "servers", "jsonSchemaDialect"];

    /// <summary>Copies visible operations, their path parameters and reachable components, omitting all unclassified operations.</summary>
    internal static JsonObject Create(JsonObject source, IReadOnlySet<string> grants)
    {
        var permissions = grants.ToHashSet(StringComparer.Ordinal);
        var inheritsSecurity = false;
        var paths = new JsonObject();
        var tags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (route, item) in source[Paths]?.AsObject() ?? throw new InvalidDataException())
        {
            var path = new JsonObject();
            foreach (var (verb, operation) in item?.AsObject() ?? throw new InvalidDataException())
            {
                if (Methods.Contains(verb, StringComparer.Ordinal) && operation is JsonObject value && Allows(value, permissions))
                {
                    inheritsSecurity |= !value.ContainsKey(Security);
                    path.Add(verb, value.DeepClone());
                    if (value[Tags] is JsonArray names)
                    {
                        tags.UnionWith(names.Select(name => name!.GetValue<string>()));
                    }
                }
            }
            if (path.Count == 0)
            {
                continue;
            }
            if (item[Parameters] is { } parameters)
            {
                path.Add(Parameters, parameters.DeepClone());
            }
            paths.Add(route, path);
        }
        var result = Metadata.Where(source.ContainsKey).ToDictionary(key => key, key => source[key]?.DeepClone());
        var document = new JsonObject(result) { [Paths] = paths };
        if (inheritsSecurity && source[Security] is { } security)
        {
            document.Add(Security, security.DeepClone());
        }
        document.Add(Components, ContractComponents.Reachable(source, document));
        if (source[Tags] is JsonArray descriptions)
        {
            document[Tags] = new JsonArray(descriptions.Where(tag => tag?[TagName] is { } name && tags.Contains(name.GetValue<string>()))
                .Select(tag => tag!.DeepClone()).ToArray());
        }
        return document;
    }

    /// <summary>Requires the scalar grant and every declared conjunctive grant. Malformed metadata cannot turn into public visibility.</summary>
    private static bool Allows(JsonObject operation, HashSet<string> permissions)
    {
        if (operation[Required] is not JsonValue requirement || !requirement.TryGetValue<string>(out var permission) ||
            string.IsNullOrWhiteSpace(permission) || !permissions.Contains(permission))
        {
            return false;
        }
        if (!operation.ContainsKey(RequiredTogether))
        {
            return true;
        }
        return operation[RequiredTogether] is JsonArray { Count: > 0 } all && all.All(value =>
            value is JsonValue scalar && scalar.TryGetValue<string>(out var grant) && !string.IsNullOrWhiteSpace(grant) && permissions.Contains(grant));
    }
}
