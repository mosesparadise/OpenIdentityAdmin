using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Completes native MFA under serialized authority and returns a result only after audited commit and disposal.</summary>
internal sealed class JourneyCompletion(PlatformDbContext database, NativeOperation operation, JourneyLocator locator,
    JourneyEligibility eligibility, CeremonyClock clock, CeremonyBudget budget, CeremonyReplay replay,
    CeremonyAdmission admission, JourneyActivation activation, UserManager<PlatformUser> users, FactorAccess access, JourneyRevocation revocation, IssuerContinuationCeremony continuations)
{
    /// <summary>Resolves locking candidates, rechecks every binding, and withholds all response material until commit succeeds.</summary>
    internal Task<Grant?> CompleteAsync(string publicId, string correlation, string purpose, string code, CancellationToken token, JourneySessionCandidate? session = null) =>
        Grant.CompleteAsync(this, publicId, correlation, purpose, code, token, session);

    /// <summary>Restricts standalone HTTP completion to interactions with no browser continuation in either direction.</summary>
    internal Task<Grant?> CompleteStandaloneAsync(string publicId, string correlation, string purpose, string code, CancellationToken token) =>
        Grant.CompleteAsync(this, publicId, correlation, purpose, code, token, boundary: JourneyCompletionBoundary.Standalone);

    /// <summary>Requires an exact issuer-browser continuation before any OTP consumption, independently of the existing issuer cookie.</summary>
    internal Task<Grant?> CompleteBrowserAsync(string publicId, string correlation, string code, IssuerContinuationRequest request, CancellationToken token) =>
        Grant.CompleteAsync(this, publicId, correlation, AdministratorPolicy.Login, code, token, boundary: JourneyCompletionBoundary.Browser(request));

    /// <summary>Completes the transaction and disposal before the private result can be converted into signing authority.</summary>
    private async Task<Completion?> CompleteCoreAsync(string publicId, string correlation, string purpose, string code, JourneySessionCandidate? session, JourneyCompletionBoundary? boundary, CancellationToken token)
    {
        var candidate = await locator.InteractionAsync(publicId, token);
        if (candidate is not { } target || purpose is not (AdministratorPolicy.Enrollment or AdministratorPolicy.Login or AdministratorPolicy.Replacement or AdministratorPolicy.Regeneration))
        {
            return null;
        }
        var plan = new CeremonyLockPlan(purpose is AdministratorPolicy.Enrollment or AdministratorPolicy.Replacement or AdministratorPolicy.Regeneration, true, [target.Browser], [target.User]);
        Completion? result;
        await using (var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token))
        {
            var account = await CeremonyAccount.LoadAsync(database, transaction, target.User);
            var interaction = await CeremonyInteraction.ReadAsync(database, transaction, account, publicId, correlation, purpose, clock.Now);
            if (interaction is null || !await eligibility.AllowsAsync(transaction, account, interaction) ||
                !await budget.OpenAsync(transaction, account))
            {
                return null;
            }
            var authority = await JourneyAuthorization.BoundSessionAsync(database, users, clock, transaction, account, interaction.Row, session);
            if ((JourneyAuthorization.IsVoluntary(purpose) && !JourneyAuthorization.IsRecoveryStage(interaction.Row) && authority is null) ||
                (purpose == AdministratorPolicy.Replacement && (!JourneyAuthorization.IsStaged(account, interaction.Row) ||
                    !JourneyAuthorization.IsFresh(interaction.Row, clock.Now))) ||
                !await admission.HasSessionCapacityAsync(transaction, account.User.Id, authority?.PublicId))
            {
                return null;
            }
            var continuation = await continuations.ReadAsync(transaction, interaction.Row);
            if (boundary is not null && !boundary.Matches(continuation))
            {
                return null;
            }
            result = await VerifyAsync(transaction, account, interaction, code, authority);
        }
        return result;
    }

    /// <summary>Consumes only a native unused factor; failed bound proof and its audit share the transaction.</summary>
    private async Task<Completion?> VerifyAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction interaction, string code, JourneySessionProof? authority)
    {
        _ = await continuations.ReadAsync(transaction, interaction.Row);
        var installing = interaction.Row.Purpose is AdministratorPolicy.Enrollment or AdministratorPolicy.Replacement;
        var proof = await replay.VerifyAsync(transaction, account, interaction, code, installing);
        if (proof is null)
        {
            await budget.FailAsync(transaction, account, interaction);
            await transaction.CommitAsync(CeremonyEvent.Factor, false, account.User.Id, clock.Now, clock);
            return null;
        }
        authority?.RequireCurrent(transaction, account, clock.Now);
        await budget.CompleteAsync(transaction, account, proof);
        var codes = installing ? await activation.ActivateAsync(transaction, account, proof, interaction.Row.Id)
            : interaction.Row.Purpose == AdministratorPolicy.Regeneration ? await RegenerateAsync(transaction, account, interaction.Row.Id) : [];
        interaction.Row.State = AdministratorPolicy.Consumed;
        interaction.Row.StagedSeed = null;
        interaction.Row.Revision = checked(interaction.Row.Revision + 1);
        if (!clock.Observe(account.Authentication, out var now) || now < proof.VerifiedAt ||
            now >= interaction.Row.ExpiresAt || now < interaction.Row.PasswordVerifiedAt ||
            now - interaction.Row.PasswordVerifiedAt >= AdministratorPolicy.FreshnessSeconds ||
            now - proof.VerifiedAt >= AdministratorPolicy.FreshnessSeconds ||
            (interaction.Row.Purpose == AdministratorPolicy.Replacement && !JourneyAuthorization.IsFresh(interaction.Row, now)))
        {
            throw new FactorUnavailableException();
        }
        authority?.RequireTime(now);
        var session = CreateSession(account, interaction.Row, proof, now);
        database.Add(session);
        var completion = await CompleteBrowserAsync(transaction, interaction.Row, session);
        await transaction.CommitAsync(Event(interaction.Row.Purpose), true, account.User.Id, now, clock);
        return new Completion(session.PublicId, session.UserId, session.ExpiresAt, codes, completion);
    }

    /// <summary>Stages a proof only from the exact new session inside the MFA transaction; the owning grant releases it after commit/disposal.</summary>
    private async Task<string?> CompleteBrowserAsync(CeremonyTransaction transaction, AdministratorInteraction interaction, AdministratorSession session)
    {
        var row = await continuations.ReadAsync(transaction, interaction);
        if (row is null)
        {
            return null;
        }
        if (session.UserId != interaction.UserId || session.PasswordVerifiedAt != interaction.PasswordVerifiedAt ||
            session.CreatedAt < row.CreatedAt || session.FactorVerifiedAt < interaction.PasswordVerifiedAt ||
            session.ExpiresAt <= clock.Now || database.Entry(session).State != EntityState.Added)
        {
            throw new FactorUnavailableException();
        }
        var selector = IssuerContinuationPolicy.CreateSelector();
        row.CompletionDigest = IssuerContinuationPolicy.Digest(selector)!;
        row.SessionId = session.PublicId;
        row.SessionExpiresAt = session.ExpiresAt;
        row.State = IssuerContinuationPolicy.Completed;
        row.Revision = checked(row.Revision + 1);
        row.RetainUntil = Math.Max(row.RetainUntil, checked(session.ExpiresAt + IssuerContinuationPolicy.ClockToleranceSeconds));
        if (!IssuerContinuationPolicy.Observe(row, clock.Now))
        {
            throw new FactorUnavailableException();
        }
        IssuerContinuationAudit.Complete(database, row, session.UserId);
        return selector;
    }

    /// <summary>Rotates verifier generation and all previous local authority while preserving the current factor and its replay ledger.</summary>
    private async Task<string[]> RegenerateAsync(CeremonyTransaction transaction, CeremonyAccount account, Guid preserved)
    {
        account.Enrollment.RecoveryGeneration = checked(account.Enrollment.RecoveryGeneration + 1);
        account.Enrollment.Revision = checked(account.Enrollment.Revision + 1);
        account.Authentication.Generation = checked(account.Authentication.Generation + 1);
        if (!(await users.UpdateSecurityStampAsync(account.User)).Succeeded)
        {
            throw new FactorUnavailableException();
        }
        await database.SaveChangesAsync(transaction.Token);
        await revocation.RevokeAsync(transaction, account, preserved);
        using var recovery = access.SelectRecovery(account.Enrollment, transaction.Token);
        return (await users.GenerateNewTwoFactorRecoveryCodesAsync(account.User, RecoveryVerifier.Count)
            ?? throw new FactorUnavailableException()).ToArray();
    }

    /// <summary>Maps only supported completed purposes to a fixed sanitized audit catalogue.</summary>
    private static CeremonyEvent Event(string purpose) => purpose switch
    {
        AdministratorPolicy.Enrollment => CeremonyEvent.Enrollment,
        AdministratorPolicy.Replacement => CeremonyEvent.Replacement,
        AdministratorPolicy.Regeneration => CeremonyEvent.Regeneration,
        AdministratorPolicy.Login => CeremonyEvent.Factor,
        _ => throw new FactorUnavailableException()
    };

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
    private sealed record Completion(string SessionId, string UserId, long ExpiresAt, string[] RecoveryCodes, string? BrowserCompletion);

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
        internal string? BrowserCompletion
        {
            get;
        }
        private int claimed;

        /// <summary>Captures only a successfully committed result constructed by the owning completion mechanism.</summary>
        private Grant(string sessionId, string userId, long expiresAt, string[] recoveryCodes, string? browserCompletion)
        {
            SessionId = sessionId;
            UserId = userId;
            ExpiresAt = expiresAt;
            RecoveryCodes = recoveryCodes;
            BrowserCompletion = browserCompletion;
        }

        /// <summary>Constructs signing authority only by executing the actual native completion and disposed commit path.</summary>
        internal static async Task<Grant?> CompleteAsync(JourneyCompletion owner, string publicId, string correlation,
            string purpose, string code, CancellationToken token, JourneySessionCandidate? session = null, JourneyCompletionBoundary? boundary = null)
        {
            var result = await owner.CompleteCoreAsync(publicId, correlation, purpose, code, session, boundary, token);
            return result is null ? null : new Grant(result.SessionId, result.UserId, result.ExpiresAt, result.RecoveryCodes, result.BrowserCompletion);
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
