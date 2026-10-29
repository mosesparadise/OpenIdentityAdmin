using System.Text.Json.Nodes;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApiContracts = IdentityPlatform.Admin.Api.Features.ApiContracts.Setup;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Checks the real protected API operation's native metadata independently of the BFF and synthetic contract fixtures.</summary>
public sealed class ApplicationDocumentTests
{
    private const int QueryFields = 3;
    private const int MaximumIdentifier = 400;
    private const int MaximumClientId = 100;
    private const int MaximumDisplayName = 200;
    private const string Project = "src/backend/IdentityPlatform.Admin.Api";
    private const string DocumentPath = "/openapi/{documentName}.json";
    private const string DocumentUri = "/openapi/v1.json";
    private const string Paths = "paths";
    private const string CollectionPath = "/api/v1/applications";
    private const string Get = "get";
    private const string Operation = "ListApplications";
    private const string OperationId = "operationId";
    private const string Permission = "applications.read";
    private const string PermissionExtension = "x-required-permission";
    private const string ExpectedSecurity = "[{\"AdminBearer\":[]}]";
    private const string Security = "security";
    private const string Parameters = "parameters";
    private const string MaximumSize = "100";
    private const string PageSize = "pageSize";
    private const string Name = "name";
    private const string Schema = "schema";
    private const string Maximum = "maximum";
    private const string NoStore = "no-store";
    private const string Responses = "responses";
    private const string Success = "200";
    private const string Headers = "headers";
    private const string CacheControl = "Cache-Control";
    private const string Constant = "const";
    private const string Components = "components";
    private const string Schemas = "schemas";
    private const string Summary = "ApplicationSummary";
    private const string Properties = "properties";
    private const string Id = "id";
    private const string ClientId = "clientId";
    private const string DisplayName = "displayName";
    private const string MaxLength = "maxLength";
    private const string Page = "ApplicationPage";
    private const string ExpiresAt = "expiresAt";
    private static readonly string[] Fields = [ClientId, "clientType", DisplayName, Id];

    /// <summary>Native generation exposes exact bearer/permission/query/projection metadata and no unimplemented management actions.</summary>
    [Fact]
    public async Task Given_ApplicationCollection_When_NativeContractGenerated_Then_ExplicitSafeVersionedOperationIsDescribed()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.Combine(ContractFixtureConfiguration.RepositoryRoot(), Project),
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        ApiContracts.AddApiContracts(builder);
        builder.Services.AddSingleton(TimeProvider.System);
        await using var app = builder.Build();
        app.MapApplicationList();
        app.MapOpenApi(DocumentPath).WithDocumentPerVersion();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var text = await client.GetStringAsync(DocumentUri, TestContext.Current.CancellationToken);
        Assert.Equal(text, await client.GetStringAsync(DocumentUri, TestContext.Current.CancellationToken));
        RequireContract(JsonNode.Parse(text)!);
    }

    /// <summary>Assertions use independent wire expectations, so weakening a DTO attribute or operation security cannot silently update the oracle.</summary>
    private static void RequireContract(JsonNode document)
    {
        Assert.Single(document[Paths]!.AsObject());
        var operation = document[Paths]![CollectionPath]![Get]!;
        Assert.Equal(Operation, operation[OperationId]!.GetValue<string>());
        Assert.Equal(Permission, operation[PermissionExtension]!.GetValue<string>());
        Assert.Equal(ExpectedSecurity, operation[Security]!.ToJsonString());
        Assert.Equal(QueryFields, operation[Parameters]!.AsArray().Count);
        Assert.Equal(MaximumSize, operation[Parameters]!.AsArray().Single(item => item![Name]!.GetValue<string>() == PageSize)![Schema]![Maximum]!.ToJsonString());
        Assert.Equal(NoStore, operation[Responses]![Success]![Headers]![CacheControl]![Schema]![Constant]!.GetValue<string>());
        var summary = document[Components]![Schemas]![Summary]!;
        Assert.Equal(Fields, summary[Properties]!.AsObject().Select(item => item.Key).Order().ToArray());
        Assert.Equal(MaximumIdentifier, summary[Properties]![Id]![MaxLength]!.GetValue<int>());
        Assert.Equal(MaximumClientId, summary[Properties]![ClientId]![MaxLength]!.GetValue<int>());
        Assert.Equal(MaximumDisplayName, summary[Properties]![DisplayName]![MaxLength]!.GetValue<int>());
        Assert.NotNull(document[Components]![Schemas]![Page]![Properties]![ExpiresAt]);
    }
}
