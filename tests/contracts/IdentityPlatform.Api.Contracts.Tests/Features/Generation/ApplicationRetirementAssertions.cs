using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Independently checks terminal admission, retained collections and mandatory detail discrimination on both real generated surfaces.</summary>
internal static class ApplicationRetirementAssertions
{
    private const int QueryFields = 3;

    private const int FreshSeconds = 300;

    private const string BrowserOperation = "RetireBrowserApplication";
    private const string ApiOperation = "RetireApplication";
    private const string BrowserSecurity = "[{\"BrowserSession\":[]}]";
    private const string ApiSecurity = "[{\"AdminBearer\":[]}]";
    private const string ReadPermission = "applications.read";

    private const string Api = "/api/v1";
    private const string Browser = "/bff";
    private const string Route = "/applications/{id}/retirement";
    private const string Retained = "/retired-applications";
    private const string Paths = "paths";
    private const string Post = "post";
    private const string Get = "get";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string Required = "required";
    private const string Responses = "responses";
    private const string Headers = "headers";
    private const string Schema = "schema";
    private const string Properties = "properties";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string Accepted = "202";
    private const string Cache = "Cache-Control";
    private const string Constant = "const";
    private const string NoStore = "no-store";
    private const string Security = "security";
    private const string Permission = "x-required-permission";
    private const string Permissions = "x-required-permissions";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Operation = "operationId";
    private const string IfMatch = "If-Match";
    private const string Key = "Idempotency-Key";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Version = "ETag";
    private const string Location = "Location";
    private const string Details = "ApplicationDetails";
    private const string Retired = "retired";
    private const string Retirement = "retirement";
    private const string Receipt = "ApplicationRetirementAcceptance";
    private static readonly string[] ReceiptFields = ["applicationId", "applicationVersion", "job", "requestKey", "retiredAt"];
    private static readonly string[] Statuses = ["202", "400", "401", "403", "404", "409", "412", "428", "503"];
    private static readonly string[] DualPermissions = ["applications.disable", "grants.revoke"];

    /// <summary>Requires exact dual authority, fresh MFA, finite request headers and job-specific response framing independently of implementation constants.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        var root = (browser ? Browser : string.Empty) + Api;
        var operation = document[Paths]![root + Route]![Post]!;
        Assert.Equal(browser ? BrowserOperation : ApiOperation, operation[Operation]!.GetValue<string>());
        Assert.Equal(DualPermissions, operation[Permissions]!.AsArray().Select(value => value!.GetValue<string>()).ToArray());
        Assert.Equal(FreshSeconds, operation[FreshMfa]!.GetValue<int>());
        Assert.Equal(browser ? BrowserSecurity : ApiSecurity, operation[Security]!.ToJsonString());
        Assert.Equal(Statuses, operation[Responses]!.AsObject().Select(value => value.Key).Order().ToArray());
        foreach (var name in browser ? new[] { IfMatch, Key, Csrf } : new[] { IfMatch, Key })
        {
            Assert.True(Assert.Single(operation[Parameters]!.AsArray(), parameter => parameter![Name]!.GetValue<string>() == name)![Required]!.GetValue<bool>());
        }
        foreach (var response in operation[Responses]!.AsObject())
        {
            Assert.Equal(NoStore, response.Value![Headers]![Cache]![Schema]![Constant]!.GetValue<string>());
        }
        Assert.True(operation[Responses]![Accepted]![Headers]![Version]![Required]!.GetValue<bool>());
        Assert.True(operation[Responses]![Accepted]![Headers]![Location]![Required]!.GetValue<bool>());
        RequireReadModel(document, root);
    }

    /// <summary>Requires a separate bounded read collection and explicit terminal fields without masking the ordinary opaque detail route.</summary>
    private static void RequireReadModel(JsonNode document, string root)
    {
        var retained = document[Paths]![root + Retained]![Get]!;
        Assert.Equal(ReadPermission, retained[Permission]!.GetValue<string>());
        Assert.Equal(QueryFields, retained[Parameters]!.AsArray().Count);
        var schemas = document[Components]![Schemas]!;
        Assert.Equal(ReceiptFields, schemas[Receipt]![Properties]!.AsObject().Select(value => value.Key).Order().ToArray());
        var required = schemas[Details]![Required]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.Contains(Retired, required);
        Assert.Contains(Retirement, required);
    }
}
