using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks independently authored grant permissions, headers and lossless wire values on both actual endpoint sets.</summary>
internal static class GrantContractAssertions
{
    private static readonly string[] ExpectedPermissions = ["applications.read", "grants.revoke"];
    private static readonly string[] ExpectedStates = ["queued", "processing", "stopped", "succeeded"];
    private static readonly string[] ExpectedFailures = ["none", "authority", "target", "storage", "clock"];
    private const string Api = "/api/v1/applications/{id}/grant-revocations";
    private const string Browser = "/bff";
    private const string Job = "/{jobId}";
    private const string Continue = "/continue";
    private const string Paths = "paths";
    private const string Get = "get";
    private const string Post = "post";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string Properties = "properties";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string Required = "required";
    private const string Permission = "x-required-permission";
    private const string Permissions = "x-required-permissions";
    private const string Mfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Responses = "responses";
    private const string Headers = "headers";
    private const string Schema = "schema";
    private const string Const = "const";
    private const string Security = "security";

    private const string JobSchema = "GrantJobView";
    private const string State = "state";
    private const string Enum = "enum";
    private const string Failure = "failure";
    private const string String = "string";
    private const string AuthorizationCount = "authorizationCount";
    private const string TokenCount = "tokenCount";
    private const string Type = "type";
    private const string PageSchema = "GrantJobPage";
    private const string Items = "items";
    private const string MaxItems = "maxItems";
    private const string InputSchema = "GrantRevocation";
    private const string AdditionalProperties = "additionalProperties";
    private const string RevokePermission = "grants.revoke";
    private const string BrowserSecurity = "[{\"BrowserSession\":[]}]";
    private const string BearerSecurity = "[{\"AdminBearer\":[]}]";
    private const string Id = "id";
    private const string IfMatch = "If-Match";
    private const string Key = "Idempotency-Key";
    private const string JobId = "jobId";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Accepted = "202";
    private const string Ok = "200";
    private const string NoStore = "no-store";
    private const string Cache = "Cache-Control";
    private const string Tag = "ETag";
    private const string Location = "Location";
    private const int RecentLimit = 10;
    private const int FreshSeconds = 300;

    /// <summary>Requires all four current operations without weakening the independent existing operation inventory.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        var path = (browser ? Browser : string.Empty) + Api;
        var paths = document[Paths]!;
        Write(paths[path]![Post]!, browser, job: false);
        Write(paths[path + Job + Continue]![Post]!, browser, job: true);
        foreach (var read in new[] { paths[path]![Get]!, paths[path + Job]![Get]! })
        {
            Assert.Equal(ExpectedPermissions, read[Permissions]!.AsArray().Select(value => value!.GetValue<string>()));
            Assert.Null(read[Mfa]);
        }
        var schemas = document[Components]![Schemas]!;
        var job = schemas[JobSchema]![Properties]!;
        Assert.Equal(ExpectedStates, job[State]![Enum]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Equal(ExpectedFailures, job[Failure]![Enum]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Equal(String, job[AuthorizationCount]![Type]!.GetValue<string>());
        Assert.Equal(String, job[TokenCount]![Type]!.GetValue<string>());
        Assert.Equal(RecentLimit, schemas[PageSchema]![Properties]![Items]![MaxItems]!.GetValue<int>());
        Assert.False(schemas[InputSchema]![AdditionalProperties]!.GetValue<bool>());
    }

    /// <summary>Requires acceptance after commit, exact key/version/CSRF input and no credential-bearing response metadata.</summary>
    private static void Write(JsonNode operation, bool browser, bool job)
    {
        Assert.Equal(RevokePermission, operation[Permission]!.GetValue<string>());
        Assert.Equal(FreshSeconds, operation[Mfa]!.GetValue<int>());
        Assert.Equal(browser ? BrowserSecurity : BearerSecurity, operation[Security]!.ToJsonString());
        var expected = new List<string> { Id, IfMatch, Key };
        if (job)
        {
            expected.Add(JobId);
        }
        if (browser)
        {
            expected.Add(Csrf);
        }
        var parameters = operation[Parameters]!.AsArray();
        Assert.Equal(expected.Order(), parameters.Select(value => value![Name]!.GetValue<string>()).Order());
        Assert.All(parameters, value => Assert.True(value![Required]!.GetValue<bool>()));
        var responses = operation[Responses]!.AsObject();
        Assert.True(responses.ContainsKey(Accepted));
        Assert.False(responses.ContainsKey(Ok));
        foreach (var response in responses)
        {
            Assert.Equal(NoStore, response.Value![Headers]![Cache]![Schema]![Const]!.GetValue<string>());
        }
        Assert.True(responses[Accepted]![Headers]![Tag]![Required]!.GetValue<bool>());
        Assert.True(responses[Accepted]![Headers]![Location]![Required]!.GetValue<bool>());
    }
}
