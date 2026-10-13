using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Admits only password-verified restricted interactions; it cannot issue a session or activate an account.</summary>
internal sealed class JourneyPassword(PlatformDbContext database, NativeOperation operation, UserManager<PlatformUser> users,
    CeremonyBudget budget, CeremonyClock clock, CeremonyAdmission admission, CeremonyGenerations generations,
    JourneyEligibility eligibility, JourneyLocator locator, FactorProtection protection)
{
    /// <summary>Locks fresh account state and allocates staging only after native password verification and capacity checks.</summary>
    internal async Task<JourneyPasswordResult> BeginAsync(string userName, string password, string purpose, string budgetSecret,
        string correlation, CancellationToken token, JourneySessionCandidate? session = null)
    {
        if (purpose is not (AdministratorPolicy.Enrollment or AdministratorPolicy.Login or AdministratorPolicy.Recovery or
            AdministratorPolicy.Replacement or AdministratorPolicy.Regeneration) ||
            !CeremonySecrets.IsIdentifier(budgetSecret) || !CeremonySecrets.IsIdentifier(correlation))
        {
            return new JourneyPasswordResult(null, true);
        }
        var digest = CeremonySecrets.Digest(budgetSecret);
        var candidate = await locator.PasswordAsync(userName, digest, token);
        var plan = new CeremonyLockPlan(purpose == AdministratorPolicy.Enrollment, true,
            candidate.Browser ? [digest] : [], candidate.User is null ? [] : [candidate.User]);
        await using var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token);
        if (candidate.User is null)
        {
            return new JourneyPasswordResult(null, await admission.CanAttemptAsync(transaction, budgetSecret));
        }
        var account = await CeremonyAccount.LoadAsync(database, transaction, candidate.User);
        if (!await admission.CanAdmitAsync(transaction, account, budgetSecret))
        {
            return new JourneyPasswordResult(null, false);
        }
        if (!await eligibility.AllowsAsync(transaction, account, purpose) || !await budget.OpenAsync(transaction, account))
        {
            return new JourneyPasswordResult(null, true);
        }
        var authority = await JourneyAuthorization.SessionAsync(database, users, clock, transaction, account, purpose, session);
        if (JourneyAuthorization.IsVoluntary(purpose) && authority is null)
        {
            return new JourneyPasswordResult(null, false);
        }
        var proof = await CeremonyPassword.VerifyAsync(users, budget, clock, transaction, account, purpose, password);
        if (proof is null)
        {
            await budget.FailAsync(transaction, account);
            await transaction.CommitAsync(CeremonyEvent.Password, false, account.User.Id, clock.Now);
            return new JourneyPasswordResult(null, false);
        }
        return new JourneyPasswordResult(await AdmitAsync(transaction, account, proof, purpose, budgetSecret, correlation, authority), false);
    }

    /// <summary>Stages a manager-generated random key without changing the active factor or granting password-only authority.</summary>
    private async Task<string?> AdmitAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyPassword proof,
        string purpose, string browser, string correlation, JourneySessionProof? authority)
    {
        var destination = purpose == AdministratorPolicy.Enrollment
            ? await generations.AllocateAsync(transaction, account) : account.Enrollment.FactorGeneration;
        var interaction = await admission.CreateAsync(transaction, account, browser, purpose, destination, correlation, proof);
        if (interaction is null)
        {
            return null;
        }
        if (purpose == AdministratorPolicy.Enrollment)
        {
            interaction.StagedSeed = protection.Protect(users.GenerateNewAuthenticatorKey(), account.User.Id,
                destination, FactorProtection.Staged, interaction.PublicId);
        }
        else if (purpose != AdministratorPolicy.Recovery && string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(account.User)))
        {
            throw new FactorUnavailableException();
        }
        authority?.RequireCurrent(transaction, account, clock.Now);
        interaction.SessionPublicId = authority?.PublicId;
        await transaction.CommitAsync(CeremonyEvent.Password, true, account.User.Id, clock.Now);
        return interaction.PublicId;
    }
}

/// <summary>Separates real native password work from generic denial handling without exposing eligibility to the browser.</summary>
internal sealed record JourneyPasswordResult(string? Interaction, bool NeedsDummyWork);
