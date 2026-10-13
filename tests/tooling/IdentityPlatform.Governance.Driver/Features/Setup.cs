using IdentityPlatform.Governance.Driver.Features.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Governance.Driver.Features;

/// <summary>Composes the single finite test-only operation without mapping routes or starting hosted services.</summary>
internal static class Setup
{
    /// <summary>Adds the operation dispatcher to the same scope as the real native governance services.</summary>
    internal static IServiceCollection AddFeatures(this IServiceCollection services) => services.AddScoped<DriverOperation>();
}
