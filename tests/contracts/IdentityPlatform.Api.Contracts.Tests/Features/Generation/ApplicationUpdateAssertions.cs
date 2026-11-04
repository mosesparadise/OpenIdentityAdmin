using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks endpoint-generated update schemas and independent per-action authority/precondition expectations on both HTTP surfaces.</summary>
internal static class ApplicationUpdateAssertions
{
    private const string Api = "/api/v1/applications/{id}";
    private const string Browser = "/bff/api/v1/applications/{id}";
    private const string Replacement = "/secret-replacements";
    private const string Paths = "paths";
    private const string Permission = "x-required-permission";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Security = "security";
    private const string Downstream = "x-downstream-api-version";
    private const string Operation = "operationId";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string Required = "required";
    private const string Schema = "schema";
    private const string Reference = "$ref";
    private const string Content = "content";
    private const string Json = "application/json";
    private const string Body = "requestBody";
    private const string Responses = "responses";
    private const string Headers = "headers";
    private const string Ok = "200";
    private const string Cache = "Cache-Control";
    private const string Constant = "const";
    private const string NoStore = "no-store";
    private const string Version = "ETag";
    private const string IfMatch = "If-Match";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Id = "id";
    private const string Minimum = "minLength";
    private const string Maximum = "maxLength";
    private const string EditSchema = "#/components/schemas/ApplicationEdit";
    private const string ReplacementSchema = "#/components/schemas/ApplicationSecretReplacement";
    private const string DetailsSchema = "#/components/schemas/ApplicationDetails";
    private const string CredentialSchema = "#/components/schemas/ApplicationCredentialOutput";
    private static readonly string[] Outcomes = ["200", "400", "401", "403", "404", "409", "412", "428", "503"];
    private const string ApiSecurity = "[{\"AdminBearer\":[]}]";
    private const string BrowserSecurity = "[{\"BrowserSession\":[]}]";
    private const string Write = "applications.write";
    private const string Credentials = "applications.credentials";
    private const string Put = "put";
    private const string Post = "post";
    private const string ApiEdit = "EditApplication";
    private const string BrowserEdit = "EditBrowserApplication";
    private const string ApiReplace = "ReplaceApplicationSecret";
    private const string BrowserReplace = "ReplaceBrowserApplicationSecret";
    private const int IdMaximum = 400;
    private const int VersionMinimum = 68;
    private const int VersionMaximum = 86;
    private const int CsrfMaximum = 4096;
    private const int DownstreamMajor = 1;
    private const int MfaSeconds = 300;

    /// <summary>Requires real PUT and explicit replacement operations without allowing one permission or output schema to substitute for the other.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        var path = browser ? Browser : Api;
        var edit = document[Paths]![path]![Put]!;
        var replace = document[Paths]![path + Replacement]![Post]!;
        Assert.Equal(browser ? BrowserEdit : ApiEdit, edit[Operation]!.GetValue<string>());
        Assert.Equal(browser ? BrowserReplace : ApiReplace, replace[Operation]!.GetValue<string>());
        Assert.Equal(Write, edit[Permission]!.GetValue<string>());
        Assert.Equal(Credentials, replace[Permission]!.GetValue<string>());
        Assert.Null(edit[FreshMfa]);
        Assert.Equal(MfaSeconds, replace[FreshMfa]!.GetValue<int>());
        RequireOperation(edit, browser, EditSchema, DetailsSchema);
        RequireOperation(replace, browser, ReplacementSchema, CredentialSchema);
    }

    /// <summary>Checks bounded mandatory identity/version/CSRF inputs, noncacheable complete outcomes and required committed response ETag.</summary>
    private static void RequireOperation(JsonNode operation, bool browser, string input, string output)
    {
        Assert.Equal(browser ? BrowserSecurity : ApiSecurity, operation[Security]!.ToJsonString());
        Assert.Equal(input, operation[Body]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.True(operation[Body]![Required]!.GetValue<bool>());
        Assert.Equal(Outcomes, operation[Responses]!.AsObject().Select(value => value.Key).Order());
        Assert.Equal(output, operation[Responses]![Ok]![Content]![Json]![Schema]![Reference]!.GetValue<string>());
        Assert.True(operation[Responses]![Ok]![Headers]![Version]![Required]!.GetValue<bool>());
        foreach (var response in operation[Responses]!.AsObject())
        {
            Assert.Equal(NoStore, response.Value![Headers]![Cache]![Schema]![Constant]!.GetValue<string>());
        }
        Parameter(operation, Id, 1, IdMaximum);
        Parameter(operation, IfMatch, VersionMinimum, VersionMaximum);
        if (browser)
        {
            Assert.Equal(DownstreamMajor, operation[Downstream]!.GetValue<int>());
            Parameter(operation, Csrf, 1, CsrfMaximum);
        }
    }

    /// <summary>Rejects duplicate or optional parameters and requires explicit wire bounds.</summary>
    private static void Parameter(JsonNode operation, string name, int minimum, int maximum)
    {
        var value = Assert.Single(operation[Parameters]!.AsArray(), item => item![Name]!.GetValue<string>() == name)!;
        Assert.True(value[Required]!.GetValue<bool>());
        Assert.Equal(minimum, value[Schema]![Minimum]!.GetValue<int>());
        Assert.Equal(maximum, value[Schema]![Maximum]!.GetValue<int>());
    }
}
