using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace IdentityPlatform.Testing;

/// <summary>Inspects the complete initialized native endpoint collection without adding an introspection route.</summary>
internal static class ProductionEndpointInventory
{
    private const string RootPrefix = "/";
    private const char RootSeparator = '/';
    private const string DriverAssembly = "IdentityPlatform.Governance.Driver";
    private const string MissingTemplate = "A production route has no explicit route template.";

    /// <summary>Requires exactly the independently specified route templates, including multiplicity; unexpected framework routes fail visibly.</summary>
    internal static void RequireOnly(WebApplication app, IEnumerable<string> expected)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToArray();
        Assert.NotEmpty(endpoints);
        var routes = endpoints.Select(endpoint =>
        {
            var route = Assert.IsType<RouteEndpoint>(endpoint);
            Assert.True(route.RoutePattern.RawText is not null, MissingTemplate);
            return RootPrefix + route.RoutePattern.RawText!.TrimStart(RootSeparator);
        }).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), routes);
    }

    /// <summary>Requires the actual production binary to remain independent of the test executor.</summary>
    internal static void RequireNoDriverReference(Assembly host) =>
        Assert.DoesNotContain(host.GetReferencedAssemblies(), assembly => assembly.Name == DriverAssembly);
}
