using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

// Private test-owned standard host configuration. Dispose only after all children have stopped.
internal sealed class NodeConfiguration : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "identity-node-config-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, Dictionary<string, string?>> settings = new(StringComparer.Ordinal);

    public NodeConfiguration()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Private configuration fixtures require Linux.");
        }
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public async Task ConfigureAsync(IDistributedApplicationTestingBuilder builder, CancellationToken cancellationToken)
    {
        await IssuerEnvironment.AddSecondaryAsync(builder, cancellationToken);
        foreach (var name in DistributionEvidence.Nodes)
        {
            var node = builder.CreateResourceBuilder<ProjectResource>(name);
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, node.Resource, values, cancellationToken);
            foreach (var callback in node.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            {
                await callback.Callback(context);
            }
            var configuration = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "None",
                ["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "None"
            };
            foreach (var pair in values.Where(pair => pair.Key.StartsWith("Identity__Protection__", StringComparison.Ordinal)))
            {
                configuration.Add(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value switch
                {
                    string value => value,
                    ParameterResource { Secret: true } secret => await secret.GetValueAsync(cancellationToken),
                    _ => throw new InvalidOperationException("Unexpected protection configuration source.")
                });
            }
            settings.Add(name, configuration);
            var directory = Path.Combine(root, name);
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException();
            }
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.CreateDirectory(Path.Combine(directory, "wwwroot"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Write(name);
            node.WithArgs("--contentRoot", directory);
            node.WithEnvironment(callback =>
            {
                foreach (var key in callback.EnvironmentVariables.Keys.Where(key => key.StartsWith("Identity__Protection__", StringComparison.Ordinal)).ToArray())
                {
                    callback.EnvironmentVariables.Remove(key);
                }
            });
        }
    }

    public Dictionary<string, string?> Snapshot(string node) => new(settings[node], StringComparer.Ordinal);

    public IEnumerable<string> SensitiveValues => settings.Values.SelectMany(value => value)
        .Where(pair => pair.Key.StartsWith("Identity:Protection:", StringComparison.Ordinal))
        .Select(pair => pair.Value!);

    public void Replace(string node, IReadOnlyDictionary<string, string?> replacement)
    {
        Assert.True(settings.ContainsKey(node), "Unknown configuration node.");
        settings[node] = new Dictionary<string, string?>(replacement, StringComparer.Ordinal);
        Write(node);
    }

    private void Write(string node)
    {
        var path = Path.Combine(root, node, "appsettings.json");
        var temporary = path + ".next";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(settings[node]);
        try
        {
            Assert.True(bytes.Length <= 32768, "Private host configuration exceeds its fixture bound.");
            IssuerMaterialFixture.WritePrivate(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            File.Delete(temporary);
        }
    }

    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
        Assert.False(Directory.Exists(root), "Private configuration survived fixture disposal.");
    }
}
