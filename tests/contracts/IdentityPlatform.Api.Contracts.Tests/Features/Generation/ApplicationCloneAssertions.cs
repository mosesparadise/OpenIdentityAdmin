using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks the generated clone contract against independent source-precondition, authority and new-destination expectations.</summary>
internal static class ApplicationCloneAssertions
{
    private const string BrowserOperation = "CloneBrowserApplication";
    private const string ApiOperation = "CloneApplication";
    private const string WritePermission = "applications.write";
    private const string InputReference = "#/components/schemas/ApplicationClone";
    private const string OutputReference = "#/components/schemas/ApplicationCredentialOutput";
    private const string TrueEnum = "[true]";
    private const string NoStore = "no-store";
    private const int FieldCount = 4;
    private static readonly string[] RequiredFields = ["clientId", "confirmed", "credentialExpiresAt", "displayName"];
    private const string Paths = "paths";
    private const string Post = "post";
    private const string Operation = "operationId";
    private const string Permission = "x-required-permission";
    private const string Freshness = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string IfMatch = "If-Match";
    private const string Required = "required";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Body = "requestBody";
    private const string Content = "content";
    private const string JsonType = "application/json";
    private const string Schema = "schema";
    private const string Reference = "$ref";
    private const string Responses = "responses";
    private const string Created = "201";
    private const string Headers = "headers";
    private const string Version = "ETag";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string CloneSchema = "ApplicationClone";
    private const string Properties = "properties";
    private const string Confirmed = "confirmed";
    private const string Enum = "enum";
    private const string Cache = "Cache-Control";
    private const string Const = "const";
    private const int FreshSeconds = 300;
    private static readonly string[] Rejections = ["400", "401", "403", "404", "409", "412", "428", "503"];
    private const string Api = "/api/v1/applications/{id}/clone";
    private const string Bff = "/bff/api/v1/applications/{id}/clone";

    /// <summary>Requires write plus fresh MFA, exact source If-Match, explicit confirmation and a new one-time 201 projection on both surfaces.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        var operation = document[Paths]![browser ? Bff : Api]![Post]!;
        Assert.Equal(browser ? BrowserOperation : ApiOperation, operation[Operation]!.GetValue<string>());
        Assert.Equal(WritePermission, operation[Permission]!.GetValue<string>());
        Assert.Equal(FreshSeconds, operation[Freshness]!.GetValue<int>());
        var parameters = operation[Parameters]!.AsArray();
        Assert.True(Assert.Single(parameters, value => value![Name]!.GetValue<string>() == IfMatch)![Required]!.GetValue<bool>());
        if (browser)
        {
            Assert.True(Assert.Single(parameters, value => value![Name]!.GetValue<string>() == Csrf)![Required]!.GetValue<bool>());
        }
        Assert.Equal(InputReference, operation[Body]![Content]![JsonType]![Schema]![Reference]!.GetValue<string>());
        var created = operation[Responses]![Created]!;
        Assert.Equal(OutputReference, created[Content]![JsonType]![Schema]![Reference]!.GetValue<string>());
        Assert.True(created[Headers]![Version]![Required]!.GetValue<bool>());
        var schema = document[Components]![Schemas]![CloneSchema]!;
        Assert.Equal(RequiredFields, schema[Required]!.AsArray().Select(value => value!.GetValue<string>()).Order());
        Assert.Equal(TrueEnum, schema[Properties]![Confirmed]![Enum]!.ToJsonString());
        Assert.Equal(FieldCount, schema[Properties]!.AsObject().Count);
        foreach (var status in Rejections)
        {
            Assert.Equal(NoStore, operation[Responses]![status]![Headers]![Cache]![Schema]![Const]!.GetValue<string>());
        }
    }
}
