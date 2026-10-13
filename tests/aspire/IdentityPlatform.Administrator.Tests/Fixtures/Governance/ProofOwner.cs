using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Owns an actual successful native verification and its privately created, unconsumed proof until rollback.</summary>
internal sealed class ProofOwner : IAsyncDisposable
{
    private bool disposed;
    internal GovernanceOperation Operation
    {
        get;
    }
    internal GovernanceVerification Verification
    {
        get;
    }
    internal GovernanceProof Proof
    {
        get;
    }

    /// <summary>Retains only real factory outputs and their transaction owner; submitted credentials remain outside this fixture.</summary>
    private ProofOwner(GovernanceOperation operation, GovernanceVerification verification, GovernanceProof proof)
    {
        Operation = operation;
        Verification = verification;
        Proof = proof;
    }

    /// <summary>Runs real password/TOTP/replay verification and creates proof only from its accepted native result.</summary>
    internal static async Task<ProofOwner> OpenAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        var code = await scenario.UnusedCodeAsync();
        var operation = await GovernanceOperation.OpenAsync(scenario, request);
        try
        {
            var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var verification = await operation.VerifyAsync(scenario, code);
            FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, true);
            Assert.True(verification.Accepted);
            var proof = GovernanceProof.Create(operation.Transaction, verification, operation.Get<CeremonyClock>().Now);
            return new ProofOwner(operation, verification, proof);
        }
        catch
        {
            await operation.DisposeAsync();
            throw;
        }
    }

    /// <summary>Rolls back native replay reservations and every technical lock revision before disposing dependencies.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            await Operation.DisposeAsync();
        }
    }
}
