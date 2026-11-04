using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Completes only managed-purpose human proof using shared native password, replay and failure budgets; it never creates a session.</summary>
internal sealed class ManagedAuthentication(PlatformDbContext database, UserManager<PlatformUser> users, CeremonyClock clock,
    CeremonyBudget budget, CeremonyAdmission admission, CeremonyReplay replay)
{
    internal const string Purpose = "managed-application-login";
    internal ManagedPasswordUpdate? PasswordUpdate
    {
        get; private set;
    }
    internal bool NeedsDummyWork
    {
        get; private set;
    }

    /// <summary>Verifies native password and atomically binds one separate ceremony, capped at the original managed expiry.</summary>
    internal async Task<bool> PasswordAsync(CeremonyTransaction transaction, CeremonyAccount account, ManagedLoginInteraction row,
        string browser, string correlation, string password)
    {
        if (!await admission.CanAdmitAsync(transaction, account, browser))
        {
            return false;
        }
        if (!account.IsActive)
        {
            NeedsDummyWork = true;
            return false;
        }
        if (!await budget.OpenAsync(transaction, account))
        {
            return false;
        }
        var (proof, update) = await ManagedPasswordUpdate.VerifyAsync(users, budget, clock, transaction, account, password);
        if (proof is null)
        {
            await budget.FailAsync(transaction, account);
            return false;
        }
        PasswordUpdate = update;
        var ceremony = await admission.CreateAsync(transaction, account, browser, Purpose, account.Enrollment.FactorGeneration, correlation, proof);
        if (ceremony is null)
        {
            return false;
        }
        if (string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(account.User)))
        {
            throw new FactorUnavailableException();
        }
        ceremony.ExpiresAt = Math.Min(ceremony.ExpiresAt, row.ExpiresAt);
        row.CeremonyId = ceremony.Id;
        row.CeremonyBrowserDigest = ceremony.BrowserDigest;
        row.BrowserBudgetId = ceremony.BrowserBudgetId;
        row.UserId = account.User.Id;
        row.SecurityStamp = account.User.SecurityStamp;
        row.AuthenticationGeneration = account.Authentication.Generation;
        row.FactorGeneration = account.Enrollment.FactorGeneration;
        row.EnrollmentRevision = account.Enrollment.Revision;
        row.PasswordVerifiedAt = proof.VerifiedAt;
        row.State = ManagedLoginState.PendingMfa;
        return true;
    }

    /// <summary>Consumes only the exact bound managed ceremony and a native unused OTP, without administrator completion or session allocation.</summary>
    internal async Task<bool> MfaAsync(CeremonyTransaction transaction, CeremonyAccount account, ManagedLoginInteraction row, string correlation, string code)
    {
        RequireAccount(transaction, account, row);
        var stored = await database.Set<AdministratorInteraction>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == row.CeremonyId, transaction.Token);
        var interaction = stored is null ? null : await CeremonyInteraction.ReadAsync(database, transaction, account, stored.PublicId, correlation, Purpose, clock.Now);
        if (interaction is null || !Matches(row, interaction.Row) || !await budget.OpenAsync(transaction, account))
        {
            return false;
        }
        var proof = await replay.VerifyAsync(transaction, account, interaction, code, staged: false);
        if (proof is null)
        {
            await budget.FailAsync(transaction, account, interaction);
            return false;
        }
        await budget.CompleteAsync(transaction, account, proof);
        interaction.Row.State = AdministratorPolicy.Consumed;
        interaction.Row.Revision = checked(interaction.Row.Revision + 1);
        row.MfaVerifiedAt = proof.VerifiedAt;
        row.State = ManagedLoginState.PendingConsent;
        return true;
    }

    /// <summary>Rechecks the exact password/MFA account generations before displaying or accepting consent; issuer cookies play no role.</summary>
    internal void RequireAccount(CeremonyTransaction transaction, CeremonyAccount account, ManagedLoginInteraction row)
    {
        account.RequireCurrent(transaction);
        if (!account.IsActive || !clock.Observe(account.Authentication, out var now) || !CeremonyBudget.IsAvailable(account.User, account.Authentication, now) || row.UserId != account.User.Id ||
            row.SecurityStamp != account.User.SecurityStamp || row.AuthenticationGeneration != account.Authentication.Generation ||
            row.FactorGeneration != account.Enrollment.FactorGeneration || row.EnrollmentRevision != account.Enrollment.Revision ||
            row.PasswordVerifiedAt is not { } password || password > now || now >= row.ExpiresAt ||
            row.MfaVerifiedAt is { } mfa && (mfa < password || mfa > now))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Requires every retained ceremony field, excluding unrelated administrator completion, staging and session families.</summary>
    internal static bool Matches(ManagedLoginInteraction row, AdministratorInteraction ceremony) => row.CeremonyId == ceremony.Id &&
        row.UserId == ceremony.UserId && row.SecurityStamp == ceremony.SecurityStamp && row.AuthenticationGeneration == ceremony.Generation &&
        row.FactorGeneration == ceremony.DestinationGeneration && row.EnrollmentRevision == ceremony.ExpectedEnrollmentRevision &&
        row.CeremonyBrowserDigest == ceremony.BrowserDigest && row.BrowserBudgetId == ceremony.BrowserBudgetId &&
        row.PasswordVerifiedAt == ceremony.PasswordVerifiedAt && row.ExpiresAt == ceremony.ExpiresAt && ceremony.Purpose == Purpose &&
        ceremony.SessionPublicId is null && ceremony.BrowserIntentDigest is null && ceremony.StagedSeed is null && ceremony.AuthorizationKind is null;
}
