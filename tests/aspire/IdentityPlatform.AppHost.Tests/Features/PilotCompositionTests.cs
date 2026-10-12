using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class PilotCompositionTests
{
    private const string DevelopmentArgument = "--environment=Development";
    private const string EnabledArgument = "--Pilot:Enabled=true";
    private const string DisabledArgument = "--Pilot:Enabled=false";
    private const string InvalidArgument = "--Pilot:Enabled=invalid";
    private const string ProviderArgument = "--Database:Provider=";
    private const string UsernameArgument = "--Pilot:UserName=synthetic-user";
    private const string RedirectArgument = "--Pilot:RedirectUri=https://localhost/callback";
    private const string PasswordArgument = "--Pilot:Password=synthetic-parameter-placeholder";
    private const string ClientArgument = "--Pilot:ClientSecret=synthetic-parameter-placeholder";
    private const string SigningPathArgument = "--Identity:Protection:Signing:Path=/synthetic/path.pfx";
    private const string SigningPasswordArgument = "--Identity:Protection:Signing:Password=synthetic-parameter-placeholder";
    private const string Bootstrap = "pilot-bootstrap";
    private const string PilotPrefix = "pilot-";
    private const string Auth = "auth";
    private const string Https = "https";
    private const string TransportHttp = "transport-http";
    private const string Demo = "demo";
    private const string Migrator = "migrator";
    private const string Api = "admin-api";
    private const string Bff = "admin-bff";
    private const string Worker = "worker";
    private const string PilotPassword = "Pilot__Password";
    private const string PilotSecret = "Pilot__ClientSecret";
    private const string SigningPassword = "Identity__Protection__Signing__Password";
    private const string PlatformPassword = "LocalDevelopment__PlatformPassword";
    private const string SessionsPassword = "LocalDevelopment__SessionsPassword";
    private const string SessionsConnection = "ConnectionStrings__adminsessions";
    private const string PlatformConnection = "ConnectionStrings__platform";
    private const string SessionsRole = "sessions_runtime";
    private const string PlatformRole = "platform_runtime";

    /// <summary>Preserves one-shot pilot ordering, runtime-role isolation and graph-owned secret references.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Given_ExplicitPilot_When_GraphIsComposed_Then_PrivateParametersStayWithTheirOwners(string provider)
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            [DevelopmentArgument, ProviderArgument + provider, EnabledArgument, UsernameArgument,
                RedirectArgument, PasswordArgument,
                ClientArgument, SigningPathArgument,
                SigningPasswordArgument], TestContext.Current.CancellationToken);
        var bootstrap = Assert.Single(builder.Resources.OfType<ProjectResource>(), resource => resource.Name == Bootstrap);
        Assert.NotEmpty(bootstrap.Annotations.OfType<WaitAnnotation>());
        var secrets = builder.Resources.OfType<ParameterResource>().Where(value => value.Name.StartsWith(PilotPrefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, secrets.Length);
        Assert.All(secrets, parameter => Assert.True(parameter.Secret));
        var auth = Assert.Single(builder.Resources.OfType<ProjectResource>(), resource => resource.Name == Auth);
        Assert.All(auth.Annotations.OfType<EndpointAnnotation>(), endpoint => Assert.Equal(Https, endpoint.UriScheme));
        Assert.DoesNotContain(auth.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == TransportHttp);
        Assert.Contains(auth.Annotations.OfType<WaitAnnotation>(), annotation => annotation.Resource.Name == Bootstrap);
        Assert.DoesNotContain(builder.Resources.OfType<ProjectResource>(), resource => resource.Name.Contains(Demo, StringComparison.Ordinal));
        var environments = await ReadEnvironmentsAsync(builder);
        RequireOrdering(builder, bootstrap);
        RequireRecipients(environments);
        RequireSecrets(environments);
    }

    /// <summary>Evaluates actual Aspire callbacks and checks successful one-shot dependency semantics.</summary>
    private static async Task<Dictionary<string, Dictionary<string, object>>> ReadEnvironmentsAsync(IDistributedApplicationTestingBuilder builder)
    {
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
            foreach (var wait in project.Annotations.OfType<WaitAnnotation>().Where(value => value.Resource.Name is Migrator or Bootstrap))
            {
                Assert.Equal(WaitType.WaitForCompletion, wait.WaitType);
                Assert.Equal(0, wait.ExitCode);
            }
        }
        return environments;
    }

    /// <summary>Requires migration completion before every operational host and the protected pilot initializer.</summary>
    private static void RequireOrdering(IDistributedApplicationTestingBuilder builder, ProjectResource bootstrap)
    {
        Assert.Contains(bootstrap.Annotations.OfType<WaitAnnotation>(), value => value.Resource.Name == Migrator);
        foreach (var name in new[] { Auth, Api, Bff, Worker })
        {
            var project = Assert.Single(builder.Resources.OfType<ProjectResource>(), value => value.Name == name);
            Assert.Contains(project.Annotations.OfType<WaitAnnotation>(), value => value.Resource.Name == Migrator);
        }
    }

    /// <summary>Checks every resource's credential recipients and least-privilege connection reference.</summary>
    private static void RequireRecipients(Dictionary<string, Dictionary<string, object>> environments)
    {
        foreach (var entry in environments)
        {
            var values = entry.Value;
            Assert.Equal(entry.Key == Bootstrap, values.ContainsKey(PilotPassword));
            Assert.Equal(entry.Key == Bootstrap, values.ContainsKey(PilotSecret));
            Assert.Equal(entry.Key is Auth or Bootstrap, values.ContainsKey(SigningPassword));
            Assert.Equal(entry.Key == Migrator, values.ContainsKey(PlatformPassword));
            Assert.Equal(entry.Key == Migrator, values.ContainsKey(SessionsPassword));
            if (entry.Key != Migrator)
            {
                var sessions = entry.Key == Bff;
                var key = sessions ? SessionsConnection : PlatformConnection;
                var connection = Assert.IsType<ReferenceExpression>(values[key]);
                Assert.Contains(sessions ? SessionsRole : PlatformRole, connection.Format, StringComparison.Ordinal);
                Assert.False(values.ContainsKey(sessions ? PlatformConnection : SessionsConnection));
            }
        }
    }

    /// <summary>Ensures password parameters are secret and the wrapping material is shared by reference.</summary>
    private static void RequireSecrets(Dictionary<string, Dictionary<string, object>> environments)
    {
        foreach (var key in new[] { PilotPassword, PilotSecret, SigningPassword })
        {
            var parameter = Assert.IsType<ParameterResource>(environments[Bootstrap][key]);
            Assert.True(parameter.Secret);
        }
        Assert.Same(environments[Bootstrap][SigningPassword],
            environments[Auth][SigningPassword]);
    }

    /// <summary>Explicit pilot disablement creates no bootstrap process.</summary>
    [Fact]
    public async Task Given_DisabledPilot_When_GraphIsComposed_Then_NoBootstrapIsCreated()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            [DevelopmentArgument, DisabledArgument], TestContext.Current.CancellationToken);
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == Bootstrap);
    }

    /// <summary>Invalid pilot activation fails before any resource starts.</summary>
    [Fact]
    public async Task Given_InvalidPilotFlag_When_GraphIsComposed_Then_ConfigurationIsRejected()
    {
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
        {
            await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                [DevelopmentArgument, InvalidArgument], TestContext.Current.CancellationToken);
        });
    }
}
