using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks actual endpoint-generated create/details metadata against independent wire expectations.</summary>
internal static class ApplicationEditorAssertions
{
    private const string ApiPath = "/api/v1/applications";
    private const string BrowserPath = "/bff/api/v1/applications";
    private const string Details = "/{id}";
    private const string Paths = "paths";
    private const string Get = "get";
    private const string Post = "post";
    private const string Security = "security";
    private const string Permission = "x-required-permission";
    private const string Read = "applications.read";
    private const string Write = "applications.write";
    private const string Downstream = "x-downstream-api-version";
    private const string Responses = "responses";
    private const string Headers = "headers";
    private const string Cache = "Cache-Control";
    private const string Schema = "schema";
    private const string Constant = "const";
    private const string NoStore = "no-store";
    private const string Created = "201";
    private const string Ok = "200";
    private const string Version = "ETag";
    private const string Required = "required";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Body = "requestBody";
    private const string Content = "content";
    private const string Json = "application/json";
    private const string Reference = "$ref";
    private const string CreateSchema = "#/components/schemas/ApplicationCreate";
    private const string CredentialSchema = "#/components/schemas/ApplicationCredentialOutput";
    private const string DetailsSchema = "#/components/schemas/ApplicationDetails";
    private const string BrowserSecurity = "[{\"BrowserSession\":[]}]";
    private const string ApiSecurity = "[{\"AdminBearer\":[]}]";

    /// <summary>Requires exact permission separation, native security, no-store, real DTOs, version semantics and browser CSRF metadata.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        var path = browser ? BrowserPath : ApiPath;
        var create = document[Paths]![path]![Post]!;
        var details = document[Paths]![path + Details]![Get]!;
        Assert.Equal(Write, create[Permission]!.GetValue<string>());
        Assert.Equal(Read, details[Permission]!.GetValue<string>());
        foreach (var operation in new[] { create, details })
        {
            Assert.Equal(browser ? BrowserSecurity : ApiSecurity, operation[Security]!.ToJsonString());
            if (browser)
            {
                Assert.Equal(1, operation[Downstream]!.GetValue<int>());
            }
            foreach (var response in operation[Responses]!.AsObject())
            {
                Assert.Equal(NoStore, response.Value![Headers]![Cache]![Schema]![Constant]!.GetValue<string>());
            }
        }
        Assert.Equal(CreateSchema, create[Body]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.True(create[Body]![Required]!.GetValue<bool>());
        Assert.Equal(CredentialSchema, create[Responses]![Created]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.Equal(DetailsSchema, details[Responses]![Ok]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.True(create[Responses]![Created]![Headers]![Version]![Required]!.GetValue<bool>());
        Assert.False(details[Responses]![Ok]![Headers]![Version]?[Required]?.GetValue<bool>() == true);
        if (browser)
        {
            var csrf = Assert.Single(create[Parameters]!.AsArray(), value => value![Name]!.GetValue<string>() == Csrf)!;
            Assert.True(csrf[Required]!.GetValue<bool>());
        }
    }
}
