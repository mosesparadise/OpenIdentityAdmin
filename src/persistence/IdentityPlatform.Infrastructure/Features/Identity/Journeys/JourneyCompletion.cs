using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Completes native MFA under serialized authority and returns a result only after audited commit and disposal.</summary>
internal sealed class JourneyCompletion(PlatformDbContext database, NativeOperation operation, JourneyLocator locator,
    JourneyEligibility eligibility, CeremonyClock clock, CeremonyBudget budget, CeremonyReplay replay,
    CeremonyAdmission admission, JourneyActivation activation)
{
    /// <summary>Resolves locking candidates, rechecks every binding, and withholds all response material until commit succeeds.</summary>
    internal Task<Grant?> CompleteAsync(string publicId, string correlation, string purpose, string code, CancellationToken token) =>
        Grant.CompleteAsync(this, publicId, correlation, purpose, code, token);

    /// <summary>Completes the transaction and disposal before the private result can be converted into signing authority.</summary>
    private async Task<Completion?> CompleteCoreAsync(string publicId, string correlation, string purpose, string code, CancellationToken token)
    {
        var candidate = await locator.InteractionAsync(publicId, token);
        if (candidate is not { } target || purpose is not (AdministratorPolicy.Enrollment or AdministratorPolicy.Login))
        {
            return null;
        }
        var plan = new CeremonyLockPlan(purpose == AdministratorPolicy.Enrollment, true, [target.Browser], [target.User]);
        Completion? result;
        await using (var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token))
        {
            var account = await CeremonyAccount.LoadAsync(database, transaction, target.User);
            var interaction = await CeremonyInteraction.ReadAsync(database, transaction, account, publicId, correlation, purpose, clock.Now);
            if (interaction is null || !await eligibility.AllowsAsync(transaction, account, interaction) ||
                !await budget.OpenAsync(transaction, account) || !await admission.HasSessionCapacityAsync(transaction, account.User.Id))
            {
                return null;
            }
            result = await VerifyAsync(transaction, account, interaction, code);
        }
        return result;
    }

    /// <summary>Consumes only a native unused factor; failed bound proof and its audit share the transaction.</summary>
    private async Task<Completion?> VerifyAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction interaction, string code)
    {
        var enrolling = interaction.Row.Purpose == AdministratorPolicy.Enrollment;
        var proof = await replay.VerifyAsync(transaction, account, interaction, code, enrolling);
        if (proof is null)
        {
            await budget.FailAsync(transaction, account, interaction);
            await transaction.CommitAsync(CeremonyEvent.Factor, false, account.User.Id, clock.Now);
            return null;
        }
        await budget.CompleteAsync(transaction, account, proof);
        var codes = enrolling ? await activation.ActivateAsync(transaction, account, proof) : [];
        interaction.Row.State = AdministratorPolicy.Consumed;
        interaction.Row.StagedSeed = null;
        interaction.Row.Revision = checked(interaction.Row.Revision + 1);
        if (!clock.Observe(account.Authentication, out var now) || now < proof.VerifiedAt ||
            now >= interaction.Row.ExpiresAt || now < interaction.Row.PasswordVerifiedAt ||
            now - interaction.Row.PasswordVerifiedAt >= AdministratorPolicy.FreshnessSeconds ||
            now - proof.VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        var session = CreateSession(account, interaction.Row, proof, now);
        database.Add(session);
        await transaction.CommitAsync(enrolling ? CeremonyEvent.Enrollment : CeremonyEvent.Factor, true, account.User.Id, now);
        return new Completion(session.PublicId, session.UserId, session.ExpiresAt, codes);
    }

    /// <summary>Persists actual verification times and immutable absolute expiry without encoding privilege claims.</summary>
    private static AdministratorSession CreateSession(CeremonyAccount account, AdministratorInteraction interaction, CeremonyProof proof, long now) => new()
    {
        PublicId = CeremonySecrets.Create(),
        UserId = account.User.Id,
        AuthenticationMethods = AdministratorPolicy.Methods,
        SecurityStamp = account.User.SecurityStamp!,
        Generation = account.Authentication.Generation,
        FactorGeneration = proof.Generation,
        PasswordVerifiedAt = interaction.PasswordVerifiedAt,
        FactorVerifiedAt = proof.VerifiedAt,
        CreatedAt = now,
        LastActivityAt = now,
        ExpiresAt = checked(now + AdministratorPolicy.SessionSeconds)
    };
    /// <summary>Remains private to completion until audited commit and all transaction owners have been disposed.</summary>
    private sealed record Completion(string SessionId, string UserId, long ExpiresAt, string[] RecoveryCodes);

    /// <summary>Carries committed authority without a caller-accessible constructor or mutable cloning operation.</summary>
    internal sealed class Grant
    {
        internal string SessionId
        {
            get;
        }
        internal string UserId
        {
            get;
        }
        internal long ExpiresAt
        {
            get;
        }
        internal string[] RecoveryCodes
        {
            get;
        }
        private int claimed;

        /// <summary>Captures only a successfully committed result constructed by the owning completion mechanism.</summary>
        private Grant(string sessionId, string userId, long expiresAt, string[] recoveryCodes)
        {
            SessionId = sessionId;
            UserId = userId;
            ExpiresAt = expiresAt;
            RecoveryCodes = recoveryCodes;
        }

        /// <summary>Constructs signing authority only by executing the actual native completion and disposed commit path.</summary>
        internal static async Task<Grant?> CompleteAsync(JourneyCompletion owner, string publicId, string correlation,
            string purpose, string code, CancellationToken token)
        {
            var result = await owner.CompleteCoreAsync(publicId, correlation, purpose, code, token);
            return result is null ? null : new Grant(result.SessionId, result.UserId, result.ExpiresAt, result.RecoveryCodes);
        }

        /// <summary>Allows the postcommit result to authorize only one signing attempt.</summary>
        internal void Claim()
        {
            if (Interlocked.Exchange(ref claimed, 1) != 0)
            {
                throw new FactorUnavailableException();
            }
        }
    }
}
