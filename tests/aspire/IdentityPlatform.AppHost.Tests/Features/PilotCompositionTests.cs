using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class PilotCompositionTests
{
    [Fact]
    public async Task Explicit_pilot_composition_forwards_private_parameters_only_to_their_owners()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", "--Pilot:Enabled=true", "--Pilot:UserName=synthetic-user",
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
