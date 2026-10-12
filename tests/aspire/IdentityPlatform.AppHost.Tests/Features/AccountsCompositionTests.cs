using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Checks independent activation and complete protection forwarding through the real AppHost composition.</summary>
[Trait("Category", "Harness")]
public sealed class AccountsCompositionTests
{
    private const string Development = "--environment=Development";
    private const string ProviderKey = "Database:Provider";
    private const string AccountsKey = "Identity:AdministratorAccounts:Enabled";
    private const string AccountsVariable = "Identity__AdministratorAccounts__Enabled";
    private const string PilotKey = "Pilot:Enabled";
    private const string Auth = "auth";
    private const string Bootstrap = "pilot-bootstrap";
    private const string IssuerVariable = "Identity__Issuer";
    private const string IssuerPrefix = "issuer-";
    private const string AccountsPrefix = "Identity__AdministratorAccounts";
    private const string ProtectionPrefix = "Identity:Protection:";
    private const string ProtectionVariable = "Identity__Protection";
    private const string Separator = ":";
    private const string EnvironmentSeparator = "__";
    private const string PathSuffix = ":Path";
    private const string SyntheticPath = "/synthetic/path.pfx";
    private const string KeyRingSetting = "KeyRingPath";
    private const string SyntheticRing = "/synthetic/key-ring";
    private const string PasswordSuffix = ":Password";
    private const string True = "true";
    private const string False = "false";
    private const string SyntheticPassword = "synthetic-parameter-placeholder";
    private static readonly string[] PasswordSettings = ["Signing:Password", "Encryption:Password", "Wrapping:Password",
        "StagedSigning:Password", "StagedEncryption:Password", "PreviousSigning:0:Password", "PreviousEncryption:0:Password", "PreviousWrapping:0:Password"];

    /// <summary>Independent flags enable only their intended resources; protection references are shared once per graph.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false)]
    [InlineData("PostgreSql", false, true)]
    [InlineData("PostgreSql", true, false)]
    [InlineData("PostgreSql", true, true)]
    [InlineData("SqlServer", false, false)]
    [InlineData("SqlServer", false, true)]
    [InlineData("SqlServer", true, false)]
    [InlineData("SqlServer", true, true)]
    public async Task Given_IndependentActivationFlags_When_AppHostIsComposed_Then_OnlyIntendedRecipientsReceiveAuthoritySettings(string provider, bool pilot, bool accounts)
    {
        var settings = Settings(provider, pilot, accounts);
        await using var builder = await CreateAsync(settings);
        Assert.Equal(pilot, builder.Resources.OfType<ProjectResource>().Any(resource => resource.Name == Bootstrap));
        var environments = await EnvironmentsAsync(builder);
        foreach (var entry in environments)
        {
            Assert.Equal(entry.Key == Auth, entry.Value.ContainsKey(AccountsVariable));
            Assert.Equal((entry.Key == Auth && (accounts || pilot)) || entry.Key == Bootstrap, entry.Value.ContainsKey(IssuerVariable));
            if (entry.Value.TryGetValue(IssuerVariable, out var issuer))
            {
                Assert.Equal(Auth, Assert.IsType<EndpointReference>(issuer).Resource.Name);
                RequireProtection(settings, entry.Value);
            }
            else
            {
                Assert.DoesNotContain(entry.Value.Keys, key => key.StartsWith(ProtectionVariable, StringComparison.Ordinal));
            }
        }
        var secrets = builder.Resources.OfType<ParameterResource>().Where(value => value.Name.StartsWith(IssuerPrefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(accounts || pilot ? PasswordSettings.Length : 0, secrets.Length);
        Assert.All(secrets, secret => Assert.True(secret.Secret));
        if (pilot)
        {
            foreach (var key in PasswordSettings.Select(key => Variable(ProtectionPrefix + key)))
            {
                Assert.Same(environments[Auth][key], environments[Bootstrap][key]);
            }
        }
    }

    /// <summary>Malformed activation data reaches strict host validation unchanged instead of silently becoming disabled.</summary>
    [Theory]
    [InlineData("Identity:AdministratorAccounts", "invalid")]
    [InlineData("Identity:AdministratorAccounts:Enabled", "invalid")]
    [InlineData("Identity:AdministratorAccounts:Unknown", "invalid")]
    [InlineData("Identity:AdministratorAccounts:Enabled:Nested", "invalid")]
    public async Task Given_MalformedAccountSubtree_When_AppHostIsComposed_Then_ExactInvalidSettingReachesOnlyAuthServer(string key, string value)
    {
        var settings = new Dictionary<string, string?> { [PilotKey] = False, [key] = value };
        await using var builder = await CreateAsync(settings);
        var environments = await EnvironmentsAsync(builder);
        Assert.Equal(value, environments[Auth][Variable(key)]);
        foreach (var entry in environments.Where(entry => entry.Key != Auth))
        {
            Assert.DoesNotContain(entry.Value.Keys, name => name.StartsWith(AccountsPrefix, StringComparison.Ordinal));
        }
    }

    /// <summary>Builds synthetic complete key history without starting resources or exposing actual credentials.</summary>
    private static Dictionary<string, string?> Settings(string provider, bool pilot, bool accounts)
    {
        var settings = new Dictionary<string, string?> { [ProviderKey] = provider, [PilotKey] = pilot ? True : False, [AccountsKey] = accounts ? True : False };
        foreach (var key in PasswordSettings)
        {
            settings[ProtectionPrefix + key] = SyntheticPassword;
            settings[ProtectionPrefix + key.Replace(PasswordSuffix, PathSuffix, StringComparison.Ordinal)] = SyntheticPath;
        }
        settings[ProtectionPrefix + KeyRingSetting] = SyntheticRing;
        return settings;
    }

    /// <summary>Injects test settings before production composition without mutating the resulting graph.</summary>
    private static Task<IDistributedApplicationTestingBuilder> CreateAsync(Dictionary<string, string?> settings) =>
        DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>([Development], (_, host) =>
        {
            host.Configuration ??= new ConfigurationManager();
            host.Configuration.AddInMemoryCollection(settings);
        }, TestContext.Current.CancellationToken);

    /// <summary>Evaluates native callbacks so assertions cover emitted environment references and exact recipients.</summary>
    private static async Task<Dictionary<string, Dictionary<string, object>>> EnvironmentsAsync(IDistributedApplicationTestingBuilder builder)
    {
        var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        foreach (var project in builder.Resources.OfType<ProjectResource>())
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, project, values, TestContext.Current.CancellationToken);
            foreach (var callback in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
            {
                await callback.Callback(context);
            }
            result.Add(project.Name, values);
        }
        return result;
    }

    /// <summary>Requires every configured historical, staged and active field, with passwords represented solely by secret parameters.</summary>
    private static void RequireProtection(Dictionary<string, string?> settings, Dictionary<string, object> values)
    {
        foreach (var setting in settings.Where(setting => setting.Key.StartsWith(ProtectionPrefix, StringComparison.Ordinal)))
        {
            var forwarded = values[Variable(setting.Key)];
            if (setting.Key.EndsWith(PasswordSuffix, StringComparison.Ordinal))
            {
                Assert.True(Assert.IsType<ParameterResource>(forwarded).Secret);
            }
            else
            {
                Assert.Equal(setting.Value, Assert.IsType<string>(forwarded));
            }
        }
    }

    /// <summary>Uses the documented ASP.NET configuration separator for expected environment keys.</summary>
    private static string Variable(string key) => key.Replace(Separator, EnvironmentSeparator, StringComparison.Ordinal);
}
