using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Transitions a restricted interaction in its existing slot after actual native recovery or current-factor proof.</summary>
internal sealed class JourneyTransition(PlatformDbContext database, NativeOperation operation, JourneyLocator locator,
    JourneyEligibility eligibility, CeremonyClock clock, CeremonyBudget budget, CeremonyReplay replay,
    UserManager<PlatformUser> users, FactorAccess access, CeremonyGenerations generations,
    FactorProtection protection, JourneyRevocation revocation)
{
    /// <summary>Rechecks browser, purpose, current authority and budgets before consuming the required native proof.</summary>
    internal async Task<string?> AuthorizeAsync(string publicId, string correlation, string purpose, string code,
        string nextCorrelation, CancellationToken token, JourneySessionCandidate? session = null)
    {
        var candidate = await locator.InteractionAsync(publicId, token);
        if (candidate is not { } target || purpose is not (AdministratorPolicy.Recovery or AdministratorPolicy.Replacement) ||
            !CeremonySecrets.IsIdentifier(nextCorrelation) || nextCorrelation == correlation)
        {
            return null;
        }
        var plan = new CeremonyLockPlan(true, false, [target.Browser], [target.User]);
        await using var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, target.User);
        var interaction = await CeremonyInteraction.ReadAsync(database, transaction, account, publicId, correlation, purpose, clock.Now);
        if (interaction is null || interaction.Row.AuthorizationKind is not null || interaction.Row.StagedSeed is not null ||
            !await eligibility.AllowsAsync(transaction, account, interaction) || !await budget.OpenAsync(transaction, account))
        {
            return null;
        }
        var authority = await JourneyAuthorization.BoundSessionAsync(database, users, clock, transaction, account, interaction.Row, session);
        if (purpose == AdministratorPolicy.Replacement && authority is null)
        {
            return null;
        }
        var verifiedAt = await VerifyAsync(transaction, account, interaction, code);
        if (verifiedAt is null)
        {
            await budget.FailAsync(transaction, account, interaction);
            await transaction.CommitAsync(CeremonyEvent.Factor, false, account.User.Id, clock.Now);
            return null;
        }
        authority?.RequireCurrent(transaction, account, clock.Now);
        return await StageAsync(transaction, account, interaction, verifiedAt.Value, nextCorrelation, authority);
    }

    /// <summary>Keeps recovery proof distinct and retains a voluntary action's combined budget until new-factor completion.</summary>
    private async Task<long?> VerifyAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction interaction, string code)
    {
        if (interaction.Row.Purpose == AdministratorPolicy.Recovery)
        {
            var recovery = await CeremonyRecoveryProof.RedeemAsync(database, users, access, clock, budget, transaction, account, interaction, code);
            if (recovery is null)
            {
                return null;
            }
            await budget.CompleteRecoveryAsync(transaction, account, recovery);
            return recovery.VerifiedAt;
        }
        var proof = await replay.VerifyAsync(transaction, account, interaction, code, false);
        if (proof is null)
        {
            return null;
        }
        proof.Consume(transaction, account, clock.Now);
        return proof.VerifiedAt;
    }
    /// <summary>Preserves absolute freshness and browser capacity while irrevocably replacing the predecessor's public binding.</summary>
    private async Task<string> StageAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction interaction,
        long verifiedAt, string correlation, JourneySessionProof? authority)
    {
        transaction.RequireContext(database);
        interaction.RequireCurrent(transaction, account, clock.Now);
        if (!transaction.Plan.Governance)
        {
            throw new FactorUnavailableException();
        }
        var recovering = interaction.Row.Purpose == AdministratorPolicy.Recovery;
        if (recovering)
        {
            await EnterRecoveryAsync(transaction, account, interaction.Row.Id);
        }
        var row = interaction.Row;
        row.DestinationGeneration = await generations.AllocateAsync(transaction, account);
        row.PublicId = CeremonySecrets.Create();
        row.BrowserDigest = CeremonySecrets.Digest(correlation);
        row.Purpose = AdministratorPolicy.Replacement;
        row.AuthorizationKind = recovering ? JourneyAuthorization.RecoveryProof : JourneyAuthorization.FactorProof;
        row.AuthorizationVerifiedAt = verifiedAt;
        row.SourceFactorGeneration = account.Enrollment.FactorGeneration;
        row.ExpectedEnrollmentRevision = account.Enrollment.Revision;
        row.Generation = account.Authentication.Generation;
        row.SecurityStamp = account.User.SecurityStamp!;
        row.Revision = checked(row.Revision + 1);
        row.StagedSeed = protection.Protect(users.GenerateNewAuthenticatorKey(), account.User.Id,
            row.DestinationGeneration, FactorProtection.Staged, row.PublicId);
        await database.SaveChangesAsync(transaction.Token);
        if (!clock.Observe(account.Authentication, out var now) || !CeremonyInteraction.IsCurrent(row, account, now) ||
            now < verifiedAt || now - verifiedAt >= AdministratorPolicy.FreshnessSeconds || !JourneyAuthorization.IsStaged(account, row))
        {
            throw new FactorUnavailableException();
        }
        authority?.RequireCurrent(transaction, account, now);
        await transaction.CommitAsync(recovering ? CeremonyEvent.Recovery : CeremonyEvent.Replacement, true, account.User.Id, now);
        return row.PublicId;
    }

    /// <summary>Revokes old authority without disabling recovery eligibility or deleting any unspent recovery verifier.</summary>
    private async Task EnterRecoveryAsync(CeremonyTransaction transaction, CeremonyAccount account, Guid preserved)
    {
        account.Enrollment.State = AdministratorPolicy.RecoveryRequired;
        account.Enrollment.Revision = checked(account.Enrollment.Revision + 1);
        account.Authentication.Generation = checked(account.Authentication.Generation + 1);
        if (!(await users.UpdateSecurityStampAsync(account.User)).Succeeded)
        {
            throw new FactorUnavailableException();
        }
        await database.SaveChangesAsync(transaction.Token);
        await revocation.RevokeAsync(transaction, account, preserved);
    }
}
