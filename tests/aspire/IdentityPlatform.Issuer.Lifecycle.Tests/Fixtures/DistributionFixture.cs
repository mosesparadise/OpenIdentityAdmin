using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

[CollectionDefinition("distribution", DisableParallelization = true)]
public sealed class DistributionGroup : ICollectionFixture<DistributionFixture>;

public sealed class DistributionFixture : IAsyncLifetime
{
    private readonly Dictionary<string, IssuerEnvironment> environments = new(StringComparer.Ordinal);
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async Task<IssuerEnvironment> GetAsync(string provider)
    {
        if (!environments.TryGetValue(provider, out var environment))
        {
            environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken,
                configureBeforeBuild: IssuerEnvironment.AddSecondaryAsync);
            environments.Add(provider, environment); // Collection owns cleanup even when second-node readiness fails.
            try
            {
                await environment.ReadyNodesAsync();
            }
            catch { environment.Invalidate(); throw; }
        }
        Assert.False(environment.Invalidated, "Earlier distribution recovery failed.");
        return environment;
    }

    public async ValueTask DisposeAsync()
    {
        var failed = false;
        foreach (var environment in environments.Values)
        {
            try
            {
                await environment.DisposeAsync();
            }
            catch (Exception) { failed = true; }
        }
        Assert.False(failed, "Distribution teardown failed; all resources were attempted.");
    }
}
