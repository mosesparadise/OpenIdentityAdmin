using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.AppHost.Features.Services;

/// <summary>Owns one graph's issuer protection references so account and pilot hosts share secret parameters safely.</summary>
internal sealed class IssuerProtection(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> issuer)
{
    private const string ParameterSeparator = "-";

    private const string IssuerVariable = "Identity__Issuer";
    private const string HttpsEndpoint = "https";

    private readonly Dictionary<string, IResourceBuilder<ParameterResource>> secrets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> recipients = new(StringComparer.Ordinal);
    private const string Protection = "Identity:Protection";
    private const string Separator = ":";
    private const string EnvironmentSeparator = "__";
    private const string Password = ":Password";
    private const string ParameterPrefix = "issuer-";

    /// <summary>Forwards the complete configured subtree once per recipient, preserving malformed fields for host validation.</summary>
    internal void Apply(IResourceBuilder<ProjectResource> resource)
    {
        if (!recipients.Add(resource.Resource.Name))
        {
            return;
        }
        resource.WithEnvironment(IssuerVariable, issuer.GetEndpoint(HttpsEndpoint));
        foreach (var setting in builder.Configuration.AsEnumerable().Where(setting => setting.Value is not null &&
            (setting.Key.Equals(Protection, StringComparison.OrdinalIgnoreCase) || setting.Key.StartsWith(Protection + Separator, StringComparison.OrdinalIgnoreCase))))
        {
            var variable = setting.Key.Replace(Separator, EnvironmentSeparator, StringComparison.Ordinal);
            if (setting.Key.EndsWith(Password, StringComparison.OrdinalIgnoreCase))
            {
                if (!secrets.TryGetValue(setting.Key, out var secret))
                {
                    secret = builder.AddParameter(ParameterPrefix + setting.Key.Replace(Separator, ParameterSeparator, StringComparison.Ordinal).ToLowerInvariant(),
                        () => setting.Value!, secret: true);
                    secrets.Add(setting.Key, secret);
                }
                resource.WithEnvironment(variable, secret);
            }
            else
            {
                resource.WithEnvironment(variable, setting.Value!);
            }
        }
    }
}
