using IdentityPlatform.AppHost.Features.Services;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.AppHost.Features.Accounts;

/// <summary>Enables administrator accounts independently of pilot provisioning while retaining host-side strict validation.</summary>
internal static class Setup
{
    private const string Section = "Identity:AdministratorAccounts";
    private const string Enabled = Section + ":Enabled";

    /// <summary>Forwards the exact activation subtree only to AuthServer and shares protection only for explicit activation.</summary>
    internal static void AddAccounts(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> auth, IssuerProtection protection)
    {
        const string Separator = ":";
        const string EnvironmentSeparator = "__";
        const string True = "true";
        foreach (var setting in builder.Configuration.AsEnumerable().Where(setting => setting.Value is not null &&
            (setting.Key.Equals(Section, StringComparison.OrdinalIgnoreCase) || setting.Key.StartsWith(Section + Separator, StringComparison.OrdinalIgnoreCase))))
        {
            auth.WithEnvironment(setting.Key.Replace(Separator, EnvironmentSeparator, StringComparison.Ordinal), setting.Value!);
        }
        if (string.Equals(builder.Configuration[Enabled], True, StringComparison.OrdinalIgnoreCase))
        {
            protection.Apply(auth);
        }
    }
}
