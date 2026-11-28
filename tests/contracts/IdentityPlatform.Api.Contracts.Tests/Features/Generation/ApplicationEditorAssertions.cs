using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks actual endpoint-generated create/details metadata against independent wire expectations.</summary>
internal static class ApplicationEditorAssertions
{
    private const string DevicePath = "/device-registrations";
    private const string DeviceSchema = "#/components/schemas/ApplicationDeviceCreate";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string DeviceInput = "ApplicationDeviceCreate";
    private const string AdditionalProperties = "additionalProperties";
    private const string Properties = "properties";
    private const string ClientId = "clientId";
    private const string DisplayName = "displayName";
    private const string PublicBrowserPath = "/browser-registrations";
    private const string PublicBrowserSchema = "#/components/schemas/ApplicationBrowserCreate";

    private const string ApiPath = "/api/v1/applications";
    private const string BrowserPath = "/bff/api/v1/applications";
    private const string NativePath = "/native-registrations";
    private const string NativeInput = "#/components/schemas/ApplicationNativeCreate";
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
        ApplicationUpdateAssertions.Require(document, browser);
        ApplicationCloneAssertions.Require(document, browser);
        var path = browser ? BrowserPath : ApiPath;
        var native = document[Paths]![path + NativePath]![Post]!;
        var spa = document[Paths]![path + PublicBrowserPath]![Post]!;
        var device = document[Paths]![path + DevicePath]![Post]!;
        RequirePublic(spa, native, PublicBrowserSchema);
        RequirePublic(device, native, DeviceSchema);
        var fields = document[Components]![Schemas]![DeviceInput]!;
        Assert.False(fields[AdditionalProperties]!.GetValue<bool>());
        var expected = new[] { ClientId, DisplayName };
        Assert.Equal(expected, fields[Properties]!.AsObject().Select(pair => pair.Key).Order());
        Assert.Equal(expected, fields[Required]!.AsArray().Select(value => value!.GetValue<string>()).Order());
        var create = document[Paths]![path]![Post]!;
        var details = document[Paths]![path + Details]![Get]!;
        Assert.Equal(NativeInput, native[Body]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.Equal(DetailsSchema, native[Responses]![Created]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.Equal(create[Parameters]?.ToJsonString(), native[Parameters]?.ToJsonString());
        Assert.True(native[Responses]![Created]![Headers]![Version]![Required]!.GetValue<bool>());
        Assert.Equal(Write, native[Permission]!.GetValue<string>());
        Assert.Equal(Write, create[Permission]!.GetValue<string>());
        Assert.Equal(Read, details[Permission]!.GetValue<string>());
        foreach (var operation in new[] { create, native, spa, device, details })
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

    /// <summary>Checks the public creation response, required permission and browser session preconditions.</summary>
    private static void RequirePublic(JsonNode operation, JsonNode native, string schema)
    {
        Assert.Equal(schema, operation[Body]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.Equal(DetailsSchema, operation[Responses]![Created]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.Equal(Write, operation[Permission]!.GetValue<string>());
        Assert.True(operation[Responses]![Created]![Headers]![Version]![Required]!.GetValue<bool>());
        Assert.Equal(native[Parameters]?.ToJsonString(), operation[Parameters]?.ToJsonString());
    }
}
