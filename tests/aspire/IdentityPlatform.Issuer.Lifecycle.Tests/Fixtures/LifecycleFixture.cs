using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

[CollectionDefinition("lifecycle", DisableParallelization = true)]
public sealed class LifecycleGroup : ICollectionFixture<LifecycleFixture>;

public sealed class LifecycleFixture : IAsyncLifetime
{
    private readonly Dictionary<string, IssuerEnvironment> environments = new(StringComparer.Ordinal);
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async Task<IssuerEnvironment> GetAsync(string provider)
    {
        if (!environments.TryGetValue(provider, out var environment))
        {
            environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken);
            environments.Add(provider, environment);
        }
        Assert.False(environment.Invalidated, "Earlier lifecycle recovery failed.");
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
        Assert.False(failed, "Lifecycle teardown failed; all resources were attempted.");
    }
}
