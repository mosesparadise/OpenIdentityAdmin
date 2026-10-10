using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.AppHost.Features.Pilot;

public static class PilotComposition
{
    private static readonly string[] PilotKeys = ["UserName", "RedirectUri", "Password", "ClientSecret"];

    public static void AddPilot(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<ProjectResource> migrator, ReferenceExpression platformRuntime, string provider)
    {
        var enabled = builder.Configuration["Pilot:Enabled"];
        if (enabled is null || string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid pilot enablement setting.");
        }
        var bootstrap = builder.AddProject<Projects.IdentityPlatform_AuthServer>("pilot-bootstrap")
            .WithArgs("--initialize-pilot")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithEnvironment("Database__Provider", provider)
            .WithEnvironment("ConnectionStrings__platform", platformRuntime)
            .WithEnvironment("Pilot__Enabled", "true")
            .WithEnvironment("Identity__Issuer", auth.GetEndpoint("https"))
            .WaitForCompletion(migrator);
        auth.WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithEnvironment("Pilot__Enabled", "true")
            .WithEnvironment("Identity__Issuer", auth.GetEndpoint("https"))
            .WaitForCompletion(bootstrap);
        foreach (var setting in builder.Configuration.AsEnumerable().Where(setting =>
            setting.Key.StartsWith("Identity:Protection:", StringComparison.OrdinalIgnoreCase) && setting.Value is not null))
        {
            var variable = setting.Key.Replace(":", "__");
            if (setting.Key.EndsWith(":Password", StringComparison.OrdinalIgnoreCase))
            {
                var secret = builder.AddParameter("pilot-" + setting.Key.Replace(":", "-").ToLowerInvariant(), () => setting.Value!, secret: true);
                auth.WithEnvironment(variable, secret);
                bootstrap.WithEnvironment(variable, secret);
            }
            else
            {
                auth.WithEnvironment(variable, setting.Value!);
                bootstrap.WithEnvironment(variable, setting.Value!);
            }
        }
        foreach (var key in PilotKeys)
        {
            var value = builder.Configuration["Pilot:" + key];
            if (value is null)
            {
                continue; // The enabled host rejects incomplete configuration; tests may inject before startup.
            }
            if (key is "Password" or "ClientSecret")
            {
                bootstrap.WithEnvironment("Pilot__" + key, builder.AddParameter("pilot-" + key.ToLowerInvariant(), () => value, secret: true));
            }
            else
            {
                bootstrap.WithEnvironment("Pilot__" + key, value);
            }
        }
    }
}
