using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Validates persisted restricted proof bindings before attempts, material selection or mutations.</summary>
internal sealed class CeremonyInteraction
{
    private readonly CeremonyTransaction owner;
    private readonly PlatformDbContext database;
    private readonly string fingerprint;
    internal AdministratorInteraction Row
    {
        get;
    }

    /// <summary>Captures a browser-validated persisted interaction; callers cannot construct a binding from request fields.</summary>
    private CeremonyInteraction(PlatformDbContext database, CeremonyTransaction owner, AdministratorInteraction row)
    {
        this.database = database;
        this.owner = owner;
        Row = row;
        fingerprint = FactorBinding.Digest(row);
    }

    /// <summary>Revalidates transaction, tracked state, absolute expiry and every captured browser/account/purpose binding.</summary>
    internal void RequireCurrent(CeremonyTransaction current, CeremonyAccount account, long now)
    {
        current.RequireCeremony();
        account.RequireCurrent(current);
        if (!ReferenceEquals(current, owner) || database.Entry(Row).State != EntityState.Unchanged ||
            FactorBinding.Digest(Row) != fingerprint || !IsCurrent(Row, account, now))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Reloads only a canonical locator under its predeclared browser/account locks and checks every binding.</summary>
    internal static async Task<CeremonyInteraction?> ReadAsync(PlatformDbContext database, CeremonyTransaction transaction,
        CeremonyAccount account, string publicId, string? secret, string purpose, long now)
    {
        transaction.RequireContext(database);
        transaction.RequireCeremony();
        account.RequireCurrent(transaction);
        if (!CeremonySecrets.IsIdentifier(publicId) || !CeremonySecrets.IsIdentifier(secret))
        {
            return null;
        }
        var row = await database.Set<AdministratorInteraction>().SingleOrDefaultAsync(value => value.PublicId == publicId, transaction.Token);
        if (row is null || row.PublicId != publicId || row.UserId != account.User.Id || row.Purpose != purpose ||
            !transaction.Plan.Browsers.Contains(row.BrowserBudgetId, StringComparer.Ordinal) ||
            !CeremonySecrets.Matches(secret, row.BrowserDigest) || !IsCurrent(row, account, now))
        {
            return null;
        }
        var browser = await database.Set<AdministratorBrowserBudget>().SingleOrDefaultAsync(value => value.Id == row.BrowserBudgetId, transaction.Token);
        return browser is not null && browser.Id == row.BrowserBudgetId && browser.ExpiresAt > now ? new CeremonyInteraction(database, transaction, row) : null;
    }

    /// <summary>Rechecks durable state and absolute password/interaction freshness after expensive work or lock waits.</summary>
    internal static bool IsCurrent(AdministratorInteraction row, CeremonyAccount account, long now) =>
        row.UserId == account.User.Id && row.SecurityStamp == account.User.SecurityStamp &&
        row.Generation == account.Authentication.Generation && row.State == AdministratorPolicy.Pending &&
        row.Attempts < AdministratorPolicy.FailedAttempts && row.CreatedAt <= now && row.ExpiresAt > now &&
        row.PasswordVerifiedAt <= now && now - row.PasswordVerifiedAt < AdministratorPolicy.FreshnessSeconds &&
        !account.Enrollment.Suspended;
}
