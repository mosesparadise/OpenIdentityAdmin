using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Owns real-provider native journey services for fault injection that cannot be performed through browser inputs.</summary>
internal sealed class JourneyScenario : IAsyncDisposable
{
    private readonly BootstrapDatabase lease;
    private readonly IssuerMaterialFixture material;
    private readonly FactorServices services;
    private readonly AsyncServiceScope scope;
    internal PlatformDbContext Database => Get<PlatformDbContext>();
    internal BootstrapDatabase Lease => lease;
    internal FactorClock Clock
    {
        get;
    }
    internal string Browser { get; } = CeremonySecrets.Create();
    internal string Correlation { get; } = CeremonySecrets.Create();
    internal string Interaction { get; private set; } = string.Empty;
    internal string Seed { get; private set; } = string.Empty;

    /// <summary>Captures scoped native dependencies without installing a fake factor, password verifier or session.</summary>
    private JourneyScenario(BootstrapDatabase lease, IssuerMaterialFixture material, FactorServices services, FactorClock clock)
    {
        this.lease = lease;
        this.material = material;
        this.services = services;
        Clock = clock;
        scope = services.Services.CreateAsyncScope();
    }

    /// <summary>Initializes the real protected bootstrap operation, then registers isolated journey dependencies.</summary>
    internal static async Task<JourneyScenario> CreateAsync(BootstrapFixture fixture, string provider,
        Action<IServiceCollection>? configure = null)
    {
        var lease = await fixture.LeaseAsync(provider);
        var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        FactorServices? services = null;
        try
        {
            await lease.InitializeAsync(TestContext.Current.CancellationToken);
            services = new FactorServices(provider, lease, material, clock, collection =>
            {
                Register(collection);
                configure?.Invoke(collection);
            });
            return new JourneyScenario(lease, material, services, clock);
        }
        catch
        {
            if (services is not null)
            {
                await services.DisposeAsync();
            }
            material.Dispose();
            await lease.DisposeAsync();
            throw;
        }
    }

    /// <summary>Uses the same production mechanisms under test-owned composition, allowing narrowly scoped provider faults.</summary>
    private static void Register(IServiceCollection services) => services.AddAdministratorJourneys()
        .AddScoped<JourneyPassword>().AddScoped<JourneyCompletion>().AddScoped<JourneyStaging>()
        .AddScoped<JourneyActivation>().AddScoped<JourneyRevocation>().AddScoped<JourneySessions>();

    /// <summary>Resolves one scoped production mechanism for a deliberate test operation.</summary>
    internal T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Creates actual password-bound random enrollment staging without minting a session.</summary>
    internal async Task StageAsync()
    {
        const string Enrollment = "Enrollment";
        var result = await Get<JourneyPassword>().BeginAsync(lease.Username, lease.Password, Enrollment, Browser, Correlation,
            TestContext.Current.CancellationToken);
        Assert.NotNull(result.Interaction);
        Interaction = result.Interaction;
        Seed = (await Get<JourneyStaging>().ReadAsync(Interaction, Correlation, TestContext.Current.CancellationToken))!;
        Assert.False(string.IsNullOrEmpty(Seed));
    }

    /// <summary>Confirms the real generated key through native verification and retains only the resulting committed capability.</summary>
    internal async Task<JourneyCompletion.Grant> EnrollAsync()
    {
        await StageAsync();
        const string Enrollment = "Enrollment";
        var grant = await Get<JourneyCompletion>().CompleteAsync(Interaction, Correlation, Enrollment,
            FactorOracle.Code(Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), TestContext.Current.CancellationToken);
        Assert.NotNull(grant);
        return grant;
    }

    /// <summary>Disposes scopes before protected material and leases, attempting every owner even after a cleanup failure.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await scope.DisposeAsync();
        }
        finally
        {
            try
            {
                await services.DisposeAsync();
            }
            finally
            {
                try
                {
                    material.Dispose();
                }
                finally { await lease.DisposeAsync(); }
            }
        }
    }
}
