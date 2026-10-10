using IdentityPlatform.Migrator.Features.Database;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features;

internal static class Setup
{
    internal static HostApplicationBuilder AddFeatures(this HostApplicationBuilder builder) => builder.AddDatabase();

    internal static Task<int> RunFeaturesAsync(this IHost host) => host.UpgradeDatabaseAsync();
}
