using System.Net;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using IdentityPlatform.Testing;
using ApiComposition = IdentityPlatform.Admin.Api.Composition.WebApplicationExtensions;
using BffComposition = IdentityPlatform.Admin.Bff.Composition.WebApplicationExtensions;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Documentation;

/// <summary>Requires the complete production API/BFF endpoint inventory to stay health-only while authentication prerequisites remain unavailable.</summary>
public sealed class ProductionRoutesTests
{
    private const string Alive = "/alive";
    private const string Health = "/health";

    /// <summary>Uses actual production extension methods and initialized endpoint metadata; no fixture docs or sample APIs can mask accidental exposure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ProductionManagementHost_When_Composed_Then_OnlyOperationalEndpointsExist(bool browserSurface)
    {
        await using var host = await ProductionContractHost.StartAsync(browserSurface);
        using var alive = await host.Client.GetAsync(Alive, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        ProductionEndpointInventory.RequireOnly(host.App, [Alive, Health]);
        ProductionEndpointInventory.RequireNoDriverReference(browserSurface ? typeof(BffComposition).Assembly : typeof(ApiComposition).Assembly);
    }
}
