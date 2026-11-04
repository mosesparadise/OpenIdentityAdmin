using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Freezes immutable request and account authority before any credential helper or native internal save can alter it.</summary>
internal sealed class ManagedHumanInvariant(ManagedLoginInteraction row, CeremonyAccount? account)
{
    private readonly string original = Immutable(row);
    private readonly string persisted = FactorBinding.Digest(row);
    private readonly ManagedLoginState phase = row.State;
    private readonly long revision = row.Revision;
    private readonly string password = Password(row);
    private readonly long? mfa = row.MfaVerifiedAt;
    private readonly long? consent = row.ConsentVerifiedAt;
    private readonly string? originalHash = account?.User.PasswordHash;
    private readonly string? originalStamp = account?.User.SecurityStamp;
    private readonly string authority = Authority(account?.User, account?.Enrollment, account?.Authentication);

    /// <summary>Rejects adopted internal-save mutations in tracked and separately queried durable state before constructing a transition receipt.</summary>
    internal async Task RequireAsync(PlatformDbContext database, ManagedHumanAction action, bool accepted, CeremonyTransaction ceremony, ManagedPasswordUpdate? update, CancellationToken token)
    {
        var actual = await database.Set<ManagedLoginInteraction>().AsNoTracking().SingleAsync(value => value.SelectorDigest == row.SelectorDigest, token);
        if (original != Immutable(row) || persisted != FactorBinding.Digest(actual) || row.Revision != revision ||
            phase != ManagedLoginState.PendingPassword && password != Password(row) ||
            mfa is not null && row.MfaVerifiedAt != mfa || consent is not null && row.ConsentVerifiedAt != consent ||
            row.State != (accepted ? Next(phase, action) : phase))
        {
            throw new FactorUnavailableException();
        }
        if (account is null)
        {
            return;
        }
        update?.Require(ceremony, account, originalHash, originalStamp);
        var hash = update?.Hash ?? originalHash;
        var stamp = update?.Stamp ?? originalStamp;
        var user = await database.Users.AsNoTracking().SingleAsync(value => value.Id == account.User.Id, token);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(value => value.UserId == user.Id, token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(value => value.UserId == user.Id, token);
        if (account.User.PasswordHash != hash || account.User.SecurityStamp != stamp || user.PasswordHash != hash || user.SecurityStamp != stamp ||
            authority != Authority(account.User, account.Enrollment, account.Authentication) || authority != Authority(user, enrollment, authentication))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Excludes only the explicitly supported phase, proof and observation deltas from immutable original native request authority.</summary>
    private static string Immutable(ManagedLoginInteraction value) => FactorBinding.Digest(new
    {
        value.SelectorDigest,
        value.BrowserDigest,
        value.ApplicationId,
        value.ApplicationRevision,
        value.DemandDigest,
        value.RequestId,
        value.CreatedAt,
        value.ExpiresAt,
        value.RetainUntil,
        value.RequestExpiresAt,
        value.NativeDependencyCreatedAt,
        value.NativeDependencyExpiresAt,
        value.CorrelationId,
        value.AuthorizationId,
        value.CodeId
    });

    /// <summary>Preserves every established password binding during MFA/consent, including its exact distinct-purpose ceremony.</summary>
    private static string Password(ManagedLoginInteraction value) => FactorBinding.Digest(new
    {
        value.CeremonyId,
        value.CeremonyBrowserDigest,
        value.BrowserBudgetId,
        value.UserId,
        value.SecurityStamp,
        value.AuthenticationGeneration,
        value.FactorGeneration,
        value.EnrollmentRevision,
        value.PasswordVerifiedAt
    });

    /// <summary>Names the only permitted phase delta; all read/failed operations retain their original phase.</summary>
    private static ManagedLoginState Next(ManagedLoginState phase, ManagedHumanAction action) => action switch
    {
        ManagedHumanAction.Password => ManagedLoginState.PendingMfa,
        ManagedHumanAction.Mfa => ManagedLoginState.PendingConsent,
        ManagedHumanAction.Approve => ManagedLoginState.Approved,
        ManagedHumanAction.Deny => ManagedLoginState.Denied,
        _ => phase
    };

    /// <summary>Allows native password/failure counters while freezing identity enablement, stamp and every enrolled factor/authentication generation.</summary>
    private static string Authority(PlatformUser? user, AdministratorEnrollment? enrollment, AdministratorAuthentication? authentication) =>
        FactorBinding.Digest(new
        {
            user?.Id,
            user?.IsEnabled,
            user?.TwoFactorEnabled,
            enrollment,
            authentication?.Generation,
            authentication?.LastAllocatedFactorGeneration
        });
}
