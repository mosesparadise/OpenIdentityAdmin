using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class PilotCompositionTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Explicit_pilot_composition_forwards_private_parameters_only_to_their_owners(string provider)
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={provider}", "--Pilot:Enabled=true", "--Pilot:UserName=synthetic-user",
                "--Pilot:RedirectUri=https://localhost/callback", "--Pilot:Password=synthetic-parameter-placeholder",
                "--Pilot:ClientSecret=synthetic-parameter-placeholder", "--Identity:Protection:Signing:Path=/synthetic/path.pfx",
                "--Identity:Protection:Signing:Password=synthetic-parameter-placeholder"], TestContext.Current.CancellationToken);
        var bootstrap = Assert.Single(builder.Resources.OfType<ProjectResource>(), resource => resource.Name == "pilot-bootstrap");
        Assert.NotEmpty(bootstrap.Annotations.OfType<WaitAnnotation>());
        var secrets = builder.Resources.OfType<ParameterResource>().Where(value => value.Name.StartsWith("pilot-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, secrets.Length);
        Assert.All(secrets, parameter => Assert.True(parameter.Secret));
        var auth = Assert.Single(builder.Resources.OfType<ProjectResource>(), resource => resource.Name == "auth");
        Assert.Contains(auth.Annotations.OfType<WaitAnnotation>(), annotation => annotation.Resource.Name == "pilot-bootstrap");
        Assert.DoesNotContain(builder.Resources.OfType<ProjectResource>(), resource => resource.Name.Contains("demo", StringComparison.Ordinal));
        var environments = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        foreach (var project in builder.Resources.OfType<ProjectResource>())
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, project, values, TestContext.Current.CancellationToken);
            foreach (var callback in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
            {
                await callback.Callback(context);
            }
            environments.Add(project.Name, values);
            foreach (var wait in project.Annotations.OfType<WaitAnnotation>().Where(value => value.Resource.Name is "migrator" or "pilot-bootstrap"))
            {
                Assert.Equal(WaitType.WaitForCompletion, wait.WaitType);
                Assert.Equal(0, wait.ExitCode);
            }
        }
        Assert.Contains(bootstrap.Annotations.OfType<WaitAnnotation>(), value => value.Resource.Name == "migrator");
        foreach (var name in new[] { "auth", "admin-api", "admin-bff", "worker" })
        {
            var project = Assert.Single(builder.Resources.OfType<ProjectResource>(), value => value.Name == name);
            Assert.Contains(project.Annotations.OfType<WaitAnnotation>(), value => value.Resource.Name == "migrator");
        }
        foreach (var entry in environments)
        {
            var values = entry.Value;
            Assert.Equal(entry.Key == "pilot-bootstrap", values.ContainsKey("Pilot__Password"));
            Assert.Equal(entry.Key == "pilot-bootstrap", values.ContainsKey("Pilot__ClientSecret"));
            Assert.Equal(entry.Key is "auth" or "pilot-bootstrap", values.ContainsKey("Identity__Protection__Signing__Password"));
            Assert.Equal(entry.Key == "migrator", values.ContainsKey("LocalDevelopment__PlatformPassword"));
            Assert.Equal(entry.Key == "migrator", values.ContainsKey("LocalDevelopment__SessionsPassword"));
            if (entry.Key != "migrator")
            {
                var sessions = entry.Key == "admin-bff";
                var key = sessions ? "ConnectionStrings__adminsessions" : "ConnectionStrings__platform";
                var connection = Assert.IsType<ReferenceExpression>(values[key]);
                Assert.Contains(sessions ? "sessions_runtime" : "platform_runtime", connection.Format, StringComparison.Ordinal);
                Assert.False(values.ContainsKey(sessions ? "ConnectionStrings__platform" : "ConnectionStrings__adminsessions"));
            }
        }
        foreach (var key in new[] { "Pilot__Password", "Pilot__ClientSecret", "Identity__Protection__Signing__Password" })
        {
            var parameter = Assert.IsType<ParameterResource>(environments["pilot-bootstrap"][key]);
            Assert.True(parameter.Secret);
        }
        Assert.Same(environments["pilot-bootstrap"]["Identity__Protection__Signing__Password"],
            environments["auth"]["Identity__Protection__Signing__Password"]);

    }

    [Fact]
    public async Task Explicit_false_preserves_default_operational_hosts()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", "--Pilot:Enabled=false"], TestContext.Current.CancellationToken);
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == "pilot-bootstrap");
    }

    [Fact]
    public async Task Invalid_pilot_flag_is_rejected_before_resources_start()
    {
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
        {
            await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                ["--environment=Development", "--Pilot:Enabled=invalid"], TestContext.Current.CancellationToken);
        });
    }
}
