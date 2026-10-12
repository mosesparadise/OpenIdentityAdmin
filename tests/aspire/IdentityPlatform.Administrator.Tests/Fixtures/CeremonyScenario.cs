using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Adapts existing synthetic factor fixtures to canonical ceremony bindings without claiming browser authentication.</summary>
internal sealed class CeremonyScenario(FactorScenario factors)
{
    internal string Secret { get; } = CeremonySecrets.Create();
    internal string BudgetSecret { get; } = CeremonySecrets.Create();
    internal AdministratorInteraction Interaction { get; private set; } = null!;
    internal string UserId => factors.User.Id;
    internal CancellationToken Token => factors.Token;

    /// <summary>Seeds canonical correlation and a fixed independent RFC seed for native mechanism tests.</summary>
    internal async Task StageAsync()
    {
        Interaction = await factors.StageAsync();
        Interaction.PublicId = CeremonySecrets.Create();
        Interaction.BrowserDigest = CeremonySecrets.Digest(Secret);
        Interaction.StagedSeed = factors.Protection.Protect(FactorScenario.Seed, UserId, 1, FactorProtection.Staged, Interaction.PublicId);
        await factors.Database.SaveChangesAsync(Token);
    }

    /// <summary>Opens the real coordinator after discovering the existing persisted browser/account identifiers.</summary>
    internal Task<CeremonyTransaction> OpenAsync(bool admission = false, bool governance = false, CancellationToken? token = null) =>
        CeremonyTransaction.OpenAsync(factors.Database, Get<NativeOperation>(), new(governance, admission,
            Interaction is null ? [] : [Interaction.BrowserBudgetId], [UserId]), token ?? Token);

    /// <summary>Reloads current state after the transaction has acquired its ordered account lock.</summary>
    internal Task<CeremonyAccount> AccountAsync(CeremonyTransaction transaction) => CeremonyAccount.LoadAsync(factors.Database, transaction, UserId);

    /// <summary>Uses the same production bound-interaction reader as the later Razor orchestration.</summary>
    internal Task<CeremonyInteraction?> ReadAsync(CeremonyTransaction transaction, CeremonyAccount account) =>
        CeremonyInteraction.ReadAsync(factors.Database, transaction, account, Interaction.PublicId, Secret, Interaction.Purpose, Get<CeremonyClock>().Now);

    /// <summary>Resolves real scoped mechanisms sharing the native factor fixture's context and durable key ring.</summary>
    internal T Get<T>() where T : notnull => factors.Services.GetRequiredService<T>();
}
