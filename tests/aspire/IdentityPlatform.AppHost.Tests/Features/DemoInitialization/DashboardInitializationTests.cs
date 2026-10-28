using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IdentityPlatform.AppHost.Tests.Features.DemoInitialization;

/// <summary>Verifies the actual dashboard wrapper, native resource aliases and opt-in launch configuration.</summary>
[Trait("Category", "Harness")]
public sealed class DashboardInitializationTests
{
    private const string ForeignName = "other-resource";
    private const string SuppliedArgument = "password";
    private const string Development = "--environment=Development";
    private const string CommandName = "initialize-administrator";
    private const string GlobalFile = "global.json";
    private const string LaunchFile = "src/aspire/IdentityPlatform.AppHost/Properties/launchSettings.json";
    private const string Profiles = "profiles";
    private const string LocalProfile = "local-demo";
    private const string DefaultProfile = "https";
    private const string EnvironmentVariables = "environmentVariables";
    private const string Enabled = "LocalDemo__Enabled";
    private const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
    private const string InitializationProperty = "local-demo-initialization";
    private const string UnavailableState = "Unavailable";

    /// <summary>Dashboard command arguments cannot bypass the fresh secret interaction.</summary>
    [Fact]
    public async Task Given_SuppliedCommandArguments_When_CallbackExecutes_Then_NoPromptOrMutationOccurs()
    {
        using var fixture = new DashboardFixture();
        await fixture.PublishAsync(fixture.Resource, DashboardFixture.InvocationId, KnownResourceStates.Running);
        var result = await fixture.ExecuteAsync(arguments: new InteractionInputCollection([new InteractionInput { Name = SuppliedArgument, InputType = InputType.SecretText, Value = DemoInteraction.Secret }]));
        Assert.False(result.Success);
        Assert.Equal(0, fixture.Interaction.Prompts);
        Assert.Empty(fixture.Process.Arguments);
        Assert.False(result.Message?.Contains(DemoInteraction.Secret, StringComparison.Ordinal) == true);
    }

    /// <summary>Neither a foreign resource alias nor a non-running original can reach Docker inspection.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_ForeignOrStoppedResource_When_CallbackExecutes_Then_OwnershipAndReadinessDeny(bool foreign)
    {
        using var fixture = new DashboardFixture();
        var resource = foreign ? new ContainerResource(ForeignName) : fixture.Resource;
        await fixture.PublishAsync(resource, DashboardFixture.InvocationId, foreign ? KnownResourceStates.Running : KnownResourceStates.Finished);
        Assert.False((await fixture.ExecuteAsync()).Success);
        Assert.Equal(0, fixture.Interaction.Prompts);
        Assert.Empty(fixture.Process.Arguments);
    }

    /// <summary>Native aliases work and busy/terminal transitions refresh dashboard availability.</summary>
    [Fact]
    public async Task Given_ReadyNativeAlias_When_CommandCompletes_Then_AvailabilityTracksBusyAndTerminalStates()
    {
        using var fixture = new DashboardFixture();
        await fixture.PublishAsync(fixture.Resource, DashboardFixture.InvocationId, KnownResourceStates.Running);
        Assert.Equal(ResourceCommandState.Enabled, fixture.State());
        fixture.Interaction.BeforeReturn = _ =>
        {
            Assert.Equal(ResourceCommandState.Disabled, fixture.State());
            AssertPublishedUnavailable(fixture);
            return Task.CompletedTask;
        };
        Assert.True((await fixture.ExecuteAsync()).Success);
        Assert.Equal(ResourceCommandState.Disabled, fixture.State());
        AssertPublishedUnavailable(fixture);
        Assert.False((await fixture.ExecuteAsync()).Success);
        Assert.Equal(2, fixture.Process.Mutations);
    }

    /// <summary>Observes native alias publication independently of the availability callback's in-memory state.</summary>
    private static void AssertPublishedUnavailable(DashboardFixture fixture)
    {
        Assert.True(fixture.Notifications.TryGetCurrentState(DashboardFixture.InvocationId, out var current));
        var property = Assert.Single(current.Snapshot.Properties, value => value.Name == InitializationProperty);
        Assert.Equal(UnavailableState, property.Value);
    }

    /// <summary>Host shutdown cancels an admitted prompt and does not accidentally submit initialization.</summary>
    [Fact]
    public async Task Given_ActivePrompt_When_AppHostStops_Then_LinkedCancellationPreventsSubmission()
    {
        using var fixture = new DashboardFixture();
        await fixture.PublishAsync(fixture.Resource, DashboardFixture.InvocationId, KnownResourceStates.Running);
        fixture.Interaction.BeforeReturn = token => { fixture.StopHost(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; };
        Assert.False((await fixture.ExecuteAsync()).Success);
        Assert.Equal(0, fixture.Process.Mutations);
    }

    /// <summary>The ordinary real AppHost graph never registers the local initialization command.</summary>
    [Fact]
    public async Task Given_OrdinaryProfile_When_AppHostIsComposed_Then_InitializationCommandIsAbsent()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>([Development], TestContext.Current.CancellationToken);
        var exposed = builder.Resources.SelectMany(resource => resource.Annotations.OfType<ResourceCommandAnnotation>()).Any(command => command.Name == CommandName);
        Assert.False(exposed);
    }

    /// <summary>The IDE profile explicitly enables Development demo while the original default remains inert.</summary>
    [Fact]
    public void Given_LaunchProfiles_When_DeveloperSelectsDemo_Then_OnlyNamedProfileEnablesLocalActivation()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, GlobalFile)))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, LaunchFile)));
        var profiles = document.RootElement.GetProperty(Profiles);
        var demo = profiles.GetProperty(LocalProfile).GetProperty(EnvironmentVariables);
        Assert.Equal("true", demo.GetProperty(Enabled).GetString());
        Assert.Equal("Development", demo.GetProperty(DotnetEnvironment).GetString());
        Assert.False(profiles.GetProperty(DefaultProfile).GetProperty(EnvironmentVariables).TryGetProperty(Enabled, out _));
    }
}
