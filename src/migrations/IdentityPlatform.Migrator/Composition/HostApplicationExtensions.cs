using IdentityPlatform.Migrator.Features;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using IdentityPlatform.Migrator.Features.Operations;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Composition;

internal static class HostApplicationExtensions
{
    internal static HostApplicationBuilder AddConfigurations(this HostApplicationBuilder builder) => builder.AddOperationConfiguration().AddLocalRoleConfiguration();

    internal static HostApplicationBuilder ConfigureServices(this HostApplicationBuilder builder) => builder.AddFeatures();

    internal static Task<int> RunApplicationAsync(this IHost host) => host.RunFeaturesAsync();
}
