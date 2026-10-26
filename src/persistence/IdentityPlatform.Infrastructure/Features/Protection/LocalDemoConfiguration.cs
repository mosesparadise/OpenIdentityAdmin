using IdentityPlatform.Core.Features.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Checks strict demo activation before any material or native service is created.</summary>
internal static class LocalDemoConfiguration
{
    private const string RootSetting = "MaterialRoot";
    /// <summary>Rejects scalar/object ambiguity and unknown local profile fields, including when disabled.</summary>
    internal static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(LocalDemoProfile.Section);
        if (section.Value is not null || section.GetChildren().Any(child =>
            (!child.Key.Equals(LocalDemoProfile.EnabledSetting, StringComparison.OrdinalIgnoreCase) && !child.Key.Equals(RootSetting, StringComparison.OrdinalIgnoreCase)) || child.GetChildren().Any()))
        {
            throw new LocalDemoConfigurationException();
        }
        return LocalDemoProfile.IsEnabled(section[LocalDemoProfile.EnabledSetting], environment.EnvironmentName);
    }
}
