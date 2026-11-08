using System.Text.Json.Nodes;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks the new assignment subresource independently of the production metadata constants and existing editor contract.</summary>
internal static class ApplicationScopeContractAssertions
{
    private const string Paths = "paths";
    private const string ApiPath = "/api/v1/applications/{id}/scopes";
    private const string BffPath = "/bff/api/v1/applications/{id}/scopes";
    private const string Get = "get";
    private const string Put = "put";
    private const string Permission = "x-required-permission";
    private const string Read = "applications.read";
    private const string Write = "applications.write";
    private const string Mfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Responses = "responses";
    private const string Ok = "200";
    private const string Headers = "headers";
    private const string ETag = "ETag";
    private const string Required = "required";
    private const string Parameters = "parameters";
    private const string Name = "name";
    private const string IfMatch = "If-Match";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string Output = "ApplicationScopes";
    private const string Properties = "properties";
    private const string Scopes = "scopes";
    private const string Maximum = "maxItems";
    private static readonly string[] Fields = ["applicationId", "expiresAt", Scopes];

    private const int FreshnessSeconds = 300;
    private const int MaximumScopes = 32;

    /// <summary>Requires independent application authority, fresh write MFA, strong read/write versions and the exact finite safe projection.</summary>
    internal static void Require(JsonNode document, bool browser)
    {
        var path = document[Paths]![browser ? BffPath : ApiPath]!;
        Assert.Equal(Read, path[Get]![Permission]!.GetValue<string>());
        Assert.Equal(Write, path[Put]![Permission]!.GetValue<string>());
        Assert.Null(path[Get]![Mfa]);
        Assert.Equal(FreshnessSeconds, path[Put]![Mfa]!.GetValue<int>());
        foreach (var method in new[] { Get, Put })
        {
            Assert.True(path[method]![Responses]![Ok]![Headers]![ETag]![Required]!.GetValue<bool>());
        }
        foreach (var header in browser ? new[] { IfMatch, Csrf } : [IfMatch])
        {
            Assert.True(path[Put]![Parameters]!.AsArray().Single(value => value![Name]!.GetValue<string>() == header)![Required]!.GetValue<bool>());
        }
        var output = document[Components]![Schemas]![Output]!;
        Assert.Equal(Fields, output[Properties]!.AsObject().Select(value => value.Key).Order(StringComparer.Ordinal));
        Assert.Equal(Fields, output[Required]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal(MaximumScopes, output[Properties]![Scopes]![Maximum]!.GetValue<int>());
    }
}
