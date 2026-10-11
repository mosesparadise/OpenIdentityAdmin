namespace IdentityPlatform.Issuer.Tests.Fixtures;

[CollectionDefinition("issuer")]
public sealed class IssuerTestGroup : ICollectionFixture<IssuerFixture>;

// Keep the growing acceptance cohort isolated without relaxing the real issuer's rate limit.
[CollectionDefinition("interaction-acceptance", DisableParallelization = true)]
public sealed class InteractionAcceptanceGroup : ICollectionFixture<IssuerFixture>;

[CollectionDefinition("exchange-acceptance", DisableParallelization = true)]
public sealed class ExchangeAcceptanceGroup : ICollectionFixture<IssuerFixture>;

[CollectionDefinition("transport-acceptance", DisableParallelization = true)]
public sealed class TransportAcceptanceGroup : ICollectionFixture<IssuerFixture>;

[CollectionDefinition("authorization-state", DisableParallelization = true)]
public sealed class AuthorizationStateGroup : ICollectionFixture<IssuerFixture>;

public sealed class IssuerFixture : IAsyncLifetime
{
    private readonly Dictionary<(string Provider, bool HttpProbe), IssuerEnvironment> environments = new();
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async Task<IssuerEnvironment> GetAsync(string provider, bool httpProbe = false)
    {
        if (environments.TryGetValue((provider, httpProbe), out var existing))
        {
            Assert.False(existing.Invalidated, "Earlier state restoration failed; refusing to reuse the issuer.");
            return existing;
        }
        var environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken, httpProbe);
        environments.Add((provider, httpProbe), environment);
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
        Assert.False(failed, "One or more issuer environments failed cleanup.");
    }
}
