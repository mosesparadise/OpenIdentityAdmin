using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Owns one real locked command scope for capability-boundary tests; no test can fabricate accepted proof.</summary>
internal sealed class GovernanceOperation : IAsyncDisposable
{
    private readonly AsyncServiceScope scope;
    internal CeremonyTransaction Transaction
    {
        get;
    }
    internal GovernanceState State
    {
        get;
    }

    /// <summary>Retains the original native scope and transaction until the test explicitly disposes them.</summary>
    private GovernanceOperation(AsyncServiceScope scope, CeremonyTransaction transaction, GovernanceState state)
    {
        this.scope = scope;
        Transaction = transaction;
        State = state;
    }

    /// <summary>Discovers and locks the actual complete account set, then rereads current authority and explicit versions.</summary>
    internal static async Task<GovernanceOperation> OpenAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        var scope = scenario.CreateScope();
        CeremonyTransaction? transaction = null;
        try
        {
            var services = scope.ServiceProvider;
            var database = services.GetRequiredService<PlatformDbContext>();
            var discovery = services.GetRequiredService<GovernanceDiscovery>();
            var token = TestContext.Current.CancellationToken;
            var plan = await discovery.DiscoverAsync(scenario.ActorId, request, token);
            transaction = await CeremonyTransaction.OpenGovernanceAsync(database, services.GetRequiredService<NativeOperation>(), plan, token);
            await discovery.RequireUnchangedAsync(transaction, scenario.ActorId, request);
            var state = await GovernanceState.ReadAsync(database, services.GetRequiredService<GovernanceGrants>(), transaction, scenario.ActorId, request);
            return new GovernanceOperation(scope, transaction, state);
        }
        catch
        {
            try
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
            finally { await scope.DisposeAsync(); }
            throw;
        }
    }

    /// <summary>Resolves the original operation's native dependency for a narrowly scoped boundary assertion.</summary>
    internal T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Obtains actual replay-reserved password/TOTP verification for the original stored full session.</summary>
    internal Task<GovernanceVerification> VerifyAsync(GovernanceScenario scenario, string code) =>
        GovernanceVerification.VerifyAsync(Get<PlatformDbContext>(), Get<UserManager<PlatformUser>>(), Get<FactorAccess>(),
            Get<CeremonyBudget>(), Get<CeremonyClock>(), Get<CeremonyReplayLedger>(), Get<GovernanceUsability>(), Transaction, State, scenario.Session, scenario.Password, code);

    /// <summary>Rolls back uncommitted native work before disposing the scoped managers and context.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Transaction.DisposeAsync();
        }
        finally { await scope.DisposeAsync(); }
    }
}
