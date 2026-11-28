using System.Collections.Frozen;
using System.Text.Json.Nodes;
using IdentityPlatform.Admin.Api.Features.Documentation;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks permission projections against each actual native generated document and independent reader grants.</summary>
internal static class PermissionContractAssertions
{
    private const string Paths = "paths";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string Security = "securitySchemes";
    private const string Read = "applications.read";
    private const string Write = "applications.write";
    private const string Grants = "grants.revoke";
    private const string Disable = "applications.disable";
    private const string Get = "get";
    private const string Post = "post";
    private const string Api = "/api/v1/applications";
    private const string Bff = "/bff/api/v1/applications";
    private const string Retire = "/{id}/retirement";
    private const string Antiforgery = "/antiforgery";
    private const string Credential = "ApplicationCredentialOutput";

    /// <summary>Verifies distinct read and write surfaces, conjunctive permission checks, component pruning and source immutability.</summary>
    internal static void Require(JsonObject original, bool browser)
    {
        var source = original.ToJsonString();
        var path = browser ? Bff : Api;
        var read = Project(original, Read);
        Assert.NotNull(read[Paths]?[path]?[Get]);
        Assert.Null(read[Paths]?[path]?[Post]);
        Assert.Null(read[Components]?[Schemas]?[Credential]);
        Assert.NotEmpty(read[Components]![Security]!.AsObject());
        var write = Project(original, Write);
        Assert.Null(write[Paths]?[path]?[Get]);
        Assert.NotNull(write[Paths]?[path]?[Post]);
        Assert.NotNull(write[Components]?[Schemas]?[Credential]);
        Assert.Null(Project(original, Disable)[Paths]?[path + Retire]?[Post]);
        Assert.NotNull(Project(original, Disable, Grants)[Paths]?[path + Retire]?[Post]);
        Assert.Empty(Project(original)[Paths]!.AsObject());
        if (browser)
        {
            Assert.Null(read[Paths]?[path + Antiforgery]);
        }
        Assert.Equal(source, original.ToJsonString());
        RequireRecursiveSchemas(original, path);
        RequireLiteralAndInheritedSecurity(original, path);
    }

    /// <summary>Verifies shared and recursive references, ordinary schema property names, and rejection of unresolved or remote references.</summary>
    private static void RequireRecursiveSchemas(JsonObject original, string path)
    {
        const string SecurityProperty = "security";
        const string LinksProperty = "links";
        const string Parent = "parent";
        const string Responses = "responses";
        const string Success = "200";
        const string Description = "description";
        const string DescriptionValue = "Synthetic projection test";
        const string Content = "content";
        const string MediaType = "application/json";
        const string Schema = "schema";
        const string Absent = "Absent";
        const string Remote = "https://example.test/schema";
        const string Nested = "nested/path";
        var source = original.DeepClone().AsObject();
        var schemas = source[Components]![Schemas]!.AsObject();
        const string root = "ProjectionTestRoot";
        const string child = "ProjectionTestShared";
        const string prefix = "#/components/schemas/";
        const string reference = "$ref";
        const string properties = "properties";
        var fields = new JsonObject
        {
            [SecurityProperty] = new JsonObject { [reference] = prefix + child },
            [LinksProperty] = new JsonObject { [reference] = prefix + child }
        };
        schemas[root] = new JsonObject { [properties] = fields };
        schemas[child] = new JsonObject { [properties] = new JsonObject { [Parent] = new JsonObject { [reference] = prefix + root } } };
        source[Paths]![path]![Get]![Responses]![Success] = new JsonObject
        {
            [Description] = DescriptionValue,
            [Content] = new JsonObject { [MediaType] = new JsonObject { [Schema] = new JsonObject { [reference] = prefix + root } } }
        };
        var kept = Project(source, Read)[Components]![Schemas]!;
        Assert.True(JsonNode.DeepEquals(schemas[root], kept[root]));
        Assert.True(JsonNode.DeepEquals(schemas[child], kept[child]));
        Assert.Null(Project(source, Write)[Components]![Schemas]?[root]);
        foreach (var unresolved in new[] { prefix + Absent, Remote, prefix + Nested })
        {
            fields[SecurityProperty]![reference] = unresolved;
            Assert.Throws<InvalidDataException>(() => Project(source, Read));
        }
    }

    /// <summary>Distinguishes literal example data from JSON references and retains only root security actually inherited by visible operations.</summary>
    private static void RequireLiteralAndInheritedSecurity(JsonObject original, string path)
    {
        const string hidden = "HiddenRootOnly";
        const string scheme = "type";
        const string schemeType = "http";
        const string rootSecurity = "security";
        const string example = "example";
        const string reference = "$ref";
        const string literal = "#/components/schemas/HiddenOnly";
        var source = original.DeepClone().AsObject();
        var operation = source[Paths]![path]![Get]!.AsObject();
        source[Components]![Security]![hidden] = new JsonObject { [scheme] = schemeType };
        source[rootSecurity] = new JsonArray(new JsonObject { [hidden] = new JsonArray() });
        operation[example] = new JsonObject { [reference] = literal };
        var scoped = Project(source, Read);
        Assert.Null(scoped[Components]?[Security]?[hidden]);
        Assert.Null(scoped[rootSecurity]);
        operation.Remove(rootSecurity);
        var inherited = Project(source, Read);
        Assert.NotNull(inherited[Components]?[Security]?[hidden]);
    }

    /// <summary>Creates an independent test permission snapshot for each projection of the same source.</summary>
    private static JsonObject Project(JsonObject source, params string[] grants) =>
        PermissionContract.Create(source, grants.ToFrozenSet(StringComparer.Ordinal));
}
