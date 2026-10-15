using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public sealed partial class IssuerEnvironment
{
    internal IReadOnlyDictionary<string, ProcessGeneration> CurrentNodeGenerations()
    {
        Assert.Equal(2, nodes.Count);
        Assert.All(nodes.Values, node => Assert.False(ProcessTreeEvidence.Terminated(node.Child), "Node generation evidence is stale."));
        return nodes.ToDictionary(pair => pair.Key, pair => pair.Value.Child, StringComparer.Ordinal);
    }
    public Task RejectSecondaryConfigurationAsync(string path, string password) => RejectSecondaryStartupAsync(path, password);

    public void RequireSafeConfigurationSnapshots(IEnumerable<string> canaries)
    {
        foreach (var name in new[] { "auth", "auth-secondary" })
        {
            Assert.True(Application.ResourceNotifications.TryGetCurrentState(name, out var state));
            var snapshot = System.Text.Json.JsonSerializer.Serialize(state!.Snapshot);
            SafeAssertions.ContainsNoSensitiveMaterial(snapshot, canaries.ToArray());
        }
    }

    public async Task RequireSafeConfigurationLogsAsync(IEnumerable<string> canaries)
    {
        var logger = Application.Services.GetRequiredService<ResourceLoggerService>();
        var model = Application.Services.GetRequiredService<DistributedApplicationModel>();
        foreach (var resource in model.Resources.Where(value => value.Name is "auth" or "auth-secondary"))
        {
            await foreach (var batch in logger.GetAllAsync(resource).WithCancellation(TestContext.Current.CancellationToken))
            {
                var output = string.Join('\n', batch.Select(line => line.Content));
                SafeAssertions.ContainsNoSensitiveMaterial(output, Password, ClientSecret);
                var index = 0;
                foreach (var canary in canaries)
                {
                    Assert.False(output.Contains(canary, StringComparison.Ordinal), $"Configuration diagnostic exposed protected canary {index}.");
                    index++;
                }
            }
        }
    }
}
