using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Combines the real native enrollment journey with explicit fixed-profile initialization and fresh issuance ownership.</summary>
internal sealed class AdmissionScenario(GovernanceScenario account) : IAsyncDisposable
{
    private const string Operator = "native-admission-fixture";
    internal GovernanceScenario Account => account;
    internal IServiceProvider Services => account.Journey.Get<IServiceProvider>();

    /// <summary>Uses production enrollment and initializer operations; neither account authority nor protocol registration is fabricated.</summary>
    internal static async Task<AdmissionScenario> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var account = await GovernanceScenario.CreateAsync(fixture, provider, services => services.AddProtocolStorageOwnership());
        try
        {
            using var material = new NativeAssertionMaterial();
            var scenario = new AdmissionScenario(account);
            Assert.True(await AdministratorRegistrationInitializer.RunAsync(scenario.Services, material.Policy, Operator, Guid.NewGuid(), TestContext.Current.CancellationToken));
            return scenario;
        }
        catch { await account.DisposeAsync(); throw; }
    }

    /// <summary>Reads source authority only after governance/profile/account locks in a fresh owning native transaction.</summary>
    internal Task<T> RunAsync<T>(Func<ProtocolStorageScope, ProtocolAdmissionResult, CancellationToken, ValueTask<T>> operation,
        long? maximumAge = null, string? session = null) => new ProtocolStorageRequest(account.Journey.Clock, TestContext.Current.CancellationToken)
        .RunAsync(Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, account.ActorId, token);
            var result = await ProtocolAdmissionProof.ReadAsync((PlatformDbContext)scope.Owner.Database, ownership,
                session ?? account.Session.SessionId, maximumAge, scope.Services.GetRequiredService<UserManager<PlatformUser>>(),
                scope.Services.GetRequiredService<NativeOperation>(), scope.Services.GetRequiredService<CeremonyClock>(), token);
            return await operation(scope, result, token);
        });

    /// <summary>Disposes the complete native journey and isolated database lease after every owned request has ended.</summary>
    public ValueTask DisposeAsync() => account.DisposeAsync();
}
