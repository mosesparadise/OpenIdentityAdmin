using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Requires independently named catalogue paths, authority, preconditions and closed finite schemas in actual native exports.</summary>
internal static class CatalogueContractAssertions
{
    private const string Api = "/api/v1/";
    private const string Browser = "/bff";
    private const string Paths = "paths";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string Properties = "properties";
    private const string Required = "required";
    private const string Additional = "additionalProperties";
    private const string Permission = "x-required-permission";
    private const string Mfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Downstream = "x-downstream-api-version";
    private const string Security = "security";
    private const string Read = ".read";
    private const string Write = ".write";
    private const string Get = "get";
    private const string Post = "post";
    private const string Put = "put";
    private const string Delete = "delete";
    private const string Detail = "/{id}";
    private const string Job = "/deletions/{jobId}";
    private const string Token = "/antiforgery";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string IfMatch = "If-Match";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Responses = "responses";
    private const string Headers = "headers";
    private const string Cache = "Cache-Control";
    private const string Schema = "schema";
    private const string Const = "const";
    private const string NoStore = "no-store";
    private const int MfaSeconds = 300;
    private const string ApiSecurity = "[{\"AdminBearer\":[]}]";
    private const string BrowserSecurity = "[{\"BrowserSession\":[]}]";
    private static readonly string[] Kinds = ["scopes", "audiences"];
    private static readonly string[] DetailsMethods = [Get, Put, Delete];
    private static readonly string[] ClosedSchemas = ["AudienceInput", "ScopeInput", "AudienceDetails", "ScopeDetails", "CatalogueDeleteInput", "CatalogueDeletionDetails", "CatalogueSummary", "CataloguePage"];

    /// <summary>Requires all implemented operations, never merely increasing the old inventory count to permit unrelated additions.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        foreach (var kind in Kinds)
        {
            var path = (browser ? Browser : string.Empty) + Api + kind;
            Operation(document[Paths]![path]![Get]!, Get, kind, browser);
            Operation(document[Paths]![path]![Post]!, Post, kind, browser);
            foreach (var method in DetailsMethods)
            {
                Operation(document[Paths]![path + Detail]![method]!, method, kind, browser);
            }
            Operation(document[Paths]![path + Job]![Get]!, Get, kind, browser);
            if (browser)
            {
                var token = document[Paths]![path + Token]![Get]!;
                Assert.Equal(BrowserSecurity, token[Security]!.ToJsonString());
                Assert.Null(token[Permission]);
                Assert.Null(token[Downstream]);
            }
        }
        foreach (var name in ClosedSchemas)
        {
            var schema = document[Components]![Schemas]![name]!;
            Assert.False(schema[Additional]!.GetValue<bool>());
            Assert.Equal(schema[Properties]!.AsObject().Select(value => value.Key).Order(), schema[Required]!.AsArray().Select(value => value!.GetValue<string>()).Order());
        }
    }

    /// <summary>Tests exact independent authentication/permission and required preconditions alongside universal no-store outcomes.</summary>
    private static void Operation(JsonNode operation, string method, string kind, bool browser)
    {
        Assert.Equal(browser ? BrowserSecurity : ApiSecurity, operation[Security]!.ToJsonString());
        Assert.Equal(kind + (method == Get ? Read : Write), operation[Permission]!.GetValue<string>());
        if (browser)
        {
            Assert.Equal(1, operation[Downstream]!.GetValue<int>());
        }
        if (method != Get)
        {
            Assert.Equal(MfaSeconds, operation[Mfa]!.GetValue<int>());
        }
        var parameters = operation[Parameters]?.AsArray() ?? new JsonArray();
        if (method is Put or Delete)
        {
            Assert.True(parameters.Single(value => value![Name]!.GetValue<string>() == IfMatch)![Required]!.GetValue<bool>());
        }
        if (browser && method != Get)
        {
            Assert.True(parameters.Single(value => value![Name]!.GetValue<string>() == Csrf)![Required]!.GetValue<bool>());
        }
        foreach (var response in operation[Responses]!.AsObject().Select(value => value.Value!))
        {
            Assert.Equal(NoStore, response[Headers]![Cache]![Schema]![Const]!.GetValue<string>());
        }
    }
}
