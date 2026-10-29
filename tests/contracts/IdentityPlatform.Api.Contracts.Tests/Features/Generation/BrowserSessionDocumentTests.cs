using System.Text.Json.Nodes;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BffContracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

/// <summary>Exports the explicitly dormant product handler and DTO, separately from both production and synthetic management documents.</summary>
public sealed class BrowserSessionDocumentTests
{
    private const int OperationCount = 2;
    private const string ApplicationsPath = "/bff/api/v1/applications";
    private const string ListOperation = "ListBrowserApplications";
    private const string ReadPermission = "applications.read";
    private const string ListSecurity = "[{\"BrowserSession\":[]}]";
    private const string OutputVariable = "IDENTITY_CONTRACT_OUTPUT";
    private const string DocumentUri = "/openapi/bff-v1.json";
    private const string Info = "info";
    private const string Version = "version";
    private const string Paths = "paths";
    private const string Get = "get";
    private const string OperationId = "operationId";
    private const string StatusExtension = "x-browser-session-status";
    private const string Security = "security";
    private const string Permission = "x-required-permission";
    private const string Downstream = "x-downstream-api-version";
    private const string Components = "components";
    private const string Schemes = "securitySchemes";
    private const string CookieScheme = "BrowserSession";
    private const string Name = "name";
    private const string Schemas = "schemas";
    private const string StatusSchema = "BrowserSessionStatus";
    private const string Properties = "properties";
    private const string Responses = "responses";
    private const string Headers = "headers";
    private const string CacheControl = "Cache-Control";
    private const string Schema = "schema";
    private const string Const = "const";
    private const string Project = "src/backend/IdentityPlatform.Admin.Bff";
    private const string DocumentPath = "/openapi/{documentName}.json";
    private const string StatusPath = "/bff/session";
    private const string ExportFile = "bff-v1.browser.json";

    /// <summary>Native generation is repeatable and contains only the real local-status contract, never callback/resume or HTML logout forms.</summary>
    [Fact]
    public async Task Given_DormantProductStatus_When_NativeContractIsGenerated_Then_OnlyMinimalVersionedMetadataIsExported()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.Combine(ContractFixtureConfiguration.RepositoryRoot(), Project),
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        BffContracts.AddApiContracts(builder);
        builder.Services.AddSingleton(TimeProvider.System);
        await using var app = builder.Build();
        app.MapBrowserSessionStatus();
        app.MapApplicationProxy();
        app.MapOpenApi(DocumentPath).WithDocumentPerVersion();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var text = await client.GetStringAsync(DocumentUri, TestContext.Current.CancellationToken);
        Assert.Equal(text, await client.GetStringAsync(DocumentUri, TestContext.Current.CancellationToken));
        RequireContract(JsonNode.Parse(text)!);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Head })
        {
            using var request = new HttpRequestMessage(method, StatusPath);
            using var denied = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, denied.StatusCode);
            Assert.Equal("no-store", denied.Headers.CacheControl?.ToString());
            Assert.Null(denied.Headers.Location);
        }
        var output = Environment.GetEnvironmentVariable(OutputVariable);
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, ExportFile), text, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Checks independent public field and no-store expectations without importing the implementation DTO shape.</summary>
    private static void RequireContract(JsonNode document)
    {
        const string ExpectedVersion = "1";
        const string ExpectedOperation = "ReadBrowserSession";
        const string ExpectedSecurity = "[{},{\"BrowserSession\":[]}]";
        const string ExpectedCookie = "__Host-identity-admin-session";
        const string Authenticated = "authenticated";
        const string ExpiresAt = "expiresAt";
        const string Subject = "subject";
        const string NoStore = "no-store";
        Assert.Equal(ExpectedVersion, document[Info]![Version]!.GetValue<string>());
        Assert.Equal(OperationCount, document[Paths]!.AsObject().Count);
        var applications = document[Paths]![ApplicationsPath]![Get]!;
        Assert.Equal(ListOperation, applications[OperationId]!.GetValue<string>());
        Assert.Equal(ReadPermission, applications[Permission]!.GetValue<string>());
        Assert.Equal(1, applications[Downstream]!.GetValue<int>());
        Assert.Equal(ListSecurity, applications[Security]!.ToJsonString());
        var operation = document[Paths]![StatusPath]![Get]!;
        Assert.Equal(ExpectedOperation, operation[OperationId]!.GetValue<string>());
        Assert.True(operation[StatusExtension]!.GetValue<bool>());
        Assert.Equal(ExpectedSecurity, operation[Security]!.ToJsonString());
        Assert.Null(operation[Permission]);
        Assert.Null(operation[Downstream]);
        Assert.Equal(ExpectedCookie, document[Components]![Schemes]![CookieScheme]![Name]!.GetValue<string>());
        var schema = document[Components]![Schemas]![StatusSchema]!;
        Assert.Equal([Authenticated, ExpiresAt, Subject], schema[Properties]!.AsObject().Select(value => value.Key).Order(StringComparer.Ordinal));
        foreach (var response in operation[Responses]!.AsObject())
        {
            Assert.Equal(NoStore, response.Value![Headers]![CacheControl]![Schema]![Const]!.GetValue<string>());
        }
    }
}
