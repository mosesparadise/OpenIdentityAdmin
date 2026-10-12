using System.Net;
using System.Text.Json.Nodes;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Generation;

public sealed class DocumentTests
{
    [Theory]
    [InlineData(false, "v1", "/api/v1/examples")]
    [InlineData(true, "bff-v1", "/bff/api/v1/examples")]
    public async Task Native_document_is_repeatable_nonempty_and_separates_surfaces(bool browserSurface, string documentName, string path)
    {
        await using var host = await ContractHost.StartAsync(browserSurface);
        var text = await host.Client.GetStringAsync($"/openapi/{documentName}.json", TestContext.Current.CancellationToken);
        Assert.Equal(text, await host.Client.GetStringAsync($"/openapi/{documentName}.json", TestContext.Current.CancellationToken));
        var document = JsonNode.Parse(text)!;
        Assert.Equal("3.1.1", document["openapi"]!.GetValue<string>());
        var paths = document["paths"]!.AsObject();
        Assert.Equal(2, paths.Count);
        Assert.Equal("ListExamples", paths[path]!["get"]!["operationId"]!.GetValue<string>());
        Assert.Equal("UpdateExample", paths[path + "/{id}"]!["put"]!["operationId"]!.GetValue<string>());
        Assert.False(paths.ContainsKey("/alive"));
        Assert.DoesNotContain("{version", text, StringComparison.Ordinal);
        Assert.Equal("/", document["servers"]![0]!["url"]!.GetValue<string>());
        Assert.NotNull(document["components"]!["schemas"]!["Example"]);
        var destination = Environment.GetEnvironmentVariable("IDENTITY_CONTRACT_OUTPUT");
        if (destination is not null)
        {
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(destination, documentName + ".fixture.json"), text, TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/v1/alive", TestContext.Current.CancellationToken)).StatusCode);
    }
}
