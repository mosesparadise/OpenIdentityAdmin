using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.KeyRotation.Activation;

[Collection("distribution")]
public sealed class ConfigurationTransitionTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Private_configuration_changes_issuance_only_after_actual_process_replacement(string provider)
    {
        using var configuration = new NodeConfiguration();
        using var replacement = new IssuerMaterialFixture();
        replacement.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature);
        using var replacementCredentials = IssuerCredentials.Load(replacement.Configuration, DateTimeOffset.UtcNow);
        await using var environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken,
            configureBeforeBuild: configuration.ConfigureAsync);
        await environment.ReadyNodesAsync();
        var original = configuration.Snapshot("auth-secondary");
        var canaries = original.Where(pair => pair.Key.StartsWith("Identity:Protection:", StringComparison.Ordinal))
            .Select(pair => pair.Value!).Concat(replacement.Settings.Where(pair => pair.Key.StartsWith("Identity:Protection:", StringComparison.Ordinal))
                .Select(pair => pair.Value!)).ToArray();
        environment.RequireSafeConfigurationSnapshots(canaries);
        var changed = new Dictionary<string, string?>(original, StringComparer.Ordinal);
        foreach (var field in new[] { "Path", "Password" })
        {
            changed["Identity:Protection:Signing:" + field] = replacement.Settings["Identity:Protection:Signing:" + field];
        }
        var before = await IssuedKeyAsync(environment, "auth-secondary");
        // Deliberate negative experiment: standard JSON reload must not activate loaded keys.
        configuration.Replace("auth-secondary", changed);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(before == await IssuedKeyAsync(environment, "auth-secondary"), "Running host activated rewritten configuration.");
        await environment.StopNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        await environment.StartNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        Assert.Equal(replacementCredentials.Signing.Thumbprint, await IssuedKeyAsync(environment, "auth-secondary"));
        environment.RequireSafeConfigurationSnapshots(canaries);
        Assert.True(before == await IssuedKeyAsync(environment, "auth"), "Peer's configured key changed.");
        await environment.StopNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        configuration.Replace("auth-secondary", original);
        await environment.StartNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        Assert.True(before == await IssuedKeyAsync(environment, "auth-secondary"), "Original configuration failed to recover.");
        await environment.StopNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        var invalid = new Dictionary<string, string?>(original, StringComparer.Ordinal);
        invalid.Remove("Identity:Protection:Signing:Password");
        configuration.Replace("auth-secondary", invalid);
        await environment.RejectSecondaryConfigurationAsync(original["Identity:Protection:Signing:Path"]!,
            original["Identity:Protection:Signing:Password"]!);
        environment.RequireSafeConfigurationSnapshots(canaries);
        configuration.Replace("auth-secondary", original);
        await environment.StartNodeAsync("auth-secondary", TestContext.Current.CancellationToken);
        Assert.True(before == await IssuedKeyAsync(environment, "auth-secondary"), "Invalid configuration prevented recovery.");
        environment.RequireSafeConfigurationSnapshots(canaries);
        await environment.RequireSafeConfigurationLogsAsync(configuration.SensitiveValues.Concat(replacement.Settings.Values.Select(value => value!)));
    }

    private static async Task<string> IssuedKeyAsync(IssuerEnvironment environment, string node)
    {
        using var browser = environment.NodeClient(node, autoRedirect: true);
        var flow = await environment.AuthorizeAsync(browser);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, node, flow);
        var access = new JsonWebToken(tokens.RootElement.GetProperty("access_token").GetString()!);
        var identity = new JsonWebToken(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(access.Kid, identity.Kid);
        using var transport = environment.NodeClient(node);
        foreach (var asset in new[] { "tokens.css", "theme.js", "login.css", "login.js" })
        {
            using var response = await transport.GetAsync(new Uri(environment.Issuer, "/assets/" + asset), TestContext.Current.CancellationToken);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.True((await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length > 0, "Account asset is empty.");
        }
        using var keys = JsonDocument.Parse(await transport.GetStringAsync(new Uri(environment.Issuer, "/connect/jwks"), TestContext.Current.CancellationToken));
        var published = keys.RootElement.GetProperty("keys");
        Assert.True(published.GetArrayLength() == 1 && published[0].GetProperty("kid").GetString() == access.Kid, "Unexpected public signing inventory.");
        return access.Kid;
    }
}
