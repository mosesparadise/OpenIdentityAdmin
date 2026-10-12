using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

internal enum KeyStage
{
    Staged, Active, Retired, Rollback, WithoutOldSigning, WithoutOldEncryption, OldOnly
}

internal sealed class KeyRotationEnvironment : IAsyncDisposable
{
    private readonly NodeConfiguration configuration = new();
    private readonly IssuerMaterialFixture replacement;

    private KeyRotationEnvironment()
    {
        try
        {
            replacement = new IssuerMaterialFixture();
        }
        catch
        {
            configuration.Dispose();
            throw;
        }
    }
    private IssuerEnvironment? environment;
    private IssuerCredentials? original;
    private IssuerCredentials? next;
    private Dictionary<string, string?> baseline = null!;
    private readonly List<string> additionalCanaries = [];
    public IssuerEnvironment Host => environment!;
    public IssuerCredentials Original => original!;
    public IssuerCredentials Next => next!;
    public string ExpectedSigning(string node) => configuration.Snapshot(node)["Identity:Protection:Signing:Path"] == baseline["Identity:Protection:Signing:Path"]
        ? Original.Signing.Thumbprint : Next.Signing.Thumbprint;
    public RetirementWindow Retirement { get; } = new();

    public static async Task<KeyRotationEnvironment> CreateAsync(string provider)
    {
        var fixture = new KeyRotationEnvironment();
        try
        {
            fixture.replacement.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, notAfter: DateTimeOffset.UtcNow.AddDays(60));
            fixture.replacement.AddCertificate("Encryption", X509KeyUsageFlags.KeyEncipherment, notAfter: DateTimeOffset.UtcNow.AddDays(60));
            fixture.environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken,
                configureBeforeBuild: fixture.configuration.ConfigureAsync);
            await fixture.Host.ReadyNodesAsync();
            foreach (var (node, generation) in fixture.Host.CurrentNodeGenerations())
            {
                fixture.Retirement.ObserveOld(node, generation);
            }
            fixture.baseline = fixture.configuration.Snapshot("auth");
            var values = new Dictionary<string, string?>(fixture.baseline, StringComparer.Ordinal)
            {
                ["Identity:Issuer"] = fixture.Host.Issuer.AbsoluteUri
            };
            fixture.original = IssuerCredentials.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), DateTimeOffset.UtcNow);
            fixture.next = IssuerCredentials.Load(fixture.replacement.Configuration, DateTimeOffset.UtcNow);
            return fixture;
        }
        catch
        {
            try
            {
                await fixture.DisposeAsync();
            }
            catch (Exception) { fixture.environment?.Invalidate(); }
            throw;
        }
    }

    public async Task ChangeAsync(string node, KeyStage stage)
    {
        var values = new Dictionary<string, string?>(baseline, StringComparer.Ordinal);
        foreach (var purpose in new[] { "Signing", "Encryption" })
        {
            var activeNew = stage is KeyStage.Active or KeyStage.Retired or KeyStage.WithoutOldEncryption or KeyStage.WithoutOldSigning;
            if (activeNew)
            {
                Copy(purpose, purpose, replacement.Settings);
                if (stage != KeyStage.Retired && !(purpose == "Signing" && stage == KeyStage.WithoutOldSigning) &&
                    !(purpose == "Encryption" && stage == KeyStage.WithoutOldEncryption))
                {
                    Copy(purpose, purpose + ":Previous:0", baseline);
                }
            }
            else if (stage is KeyStage.Staged or KeyStage.Rollback)
            {
                Copy(purpose, purpose + (stage == KeyStage.Staged ? ":Staged" : ":Previous:0"), replacement.Settings);
            }
        }
        await Host.StopNodeAsync(node, TestContext.Current.CancellationToken);
        configuration.Replace(node, values);
        await Host.StartNodeAsync(node, TestContext.Current.CancellationToken);
        if (stage is KeyStage.Staged or KeyStage.Rollback or KeyStage.OldOnly)
        {
            Retirement.ObserveOld(node, Host.CurrentNodeGenerations()[node]);
        }
        Host.RequireSafeConfigurationSnapshots(SensitiveValues());

        void Copy(string source, string target, IReadOnlyDictionary<string, string?> from)
        {
            foreach (var field in new[] { "Path", "Password" })
            {
                values[$"Identity:Protection:{target}:{field}"] = from[$"Identity:Protection:{source}:{field}"];
            }
        }
    }

    private IEnumerable<string> SensitiveValues() => baseline.Concat(replacement.Settings)
        .Where(pair => pair.Key.StartsWith("Identity:Protection:", StringComparison.Ordinal)).Select(pair => pair.Value!).Concat(additionalCanaries);

    public async Task UseControlledSigningAsync(string node, IReadOnlyDictionary<string, string?> material)
    {
        var values = new Dictionary<string, string?>(baseline, StringComparer.Ordinal);
        foreach (var field in new[] { "Path", "Password" })
        {
            values[$"Identity:Protection:Signing:Previous:0:{field}"] = baseline[$"Identity:Protection:Signing:{field}"];
            values[$"Identity:Protection:Signing:{field}"] = material[$"Identity:Protection:Signing:{field}"];
            additionalCanaries.Add(material[$"Identity:Protection:Signing:{field}"]!);
        }
        await Host.StopNodeAsync(node, TestContext.Current.CancellationToken);
        configuration.Replace(node, values);
        await Host.StartNodeAsync(node, TestContext.Current.CancellationToken);
        Host.RequireSafeConfigurationSnapshots(SensitiveValues());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (environment is not null)
            {
                try
                {
                    if (baseline is not null)
                    {
                        await Host.RequireSafeConfigurationLogsAsync(SensitiveValues());
                    }
                }
                catch (Exception failure)
                {
                    environment.Invalidate("key-rotation.log-scan", failure);
                    throw;
                }
                finally { await environment.DisposeAsync(); }
            }
        }
        finally
        {
            try
            {
                original?.Dispose();
            }
            finally
            {
                try
                {
                    next?.Dispose();
                }
                finally
                {
                    try
                    {
                        replacement.Dispose();
                    }
                    finally { configuration.Dispose(); }
                }
            }
        }
    }
}
