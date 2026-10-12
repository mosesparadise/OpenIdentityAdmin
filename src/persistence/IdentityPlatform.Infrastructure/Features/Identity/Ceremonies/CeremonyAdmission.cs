using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Counts live and retained authority separately under the installation admission and account locks.</summary>
internal sealed class CeremonyAdmission(PlatformDbContext database, CeremonyClock clock)
{
    /// <summary>Allocates a browser budget only with an admitted interaction; denial cannot leave orphan retained state.</summary>
    internal async Task<AdministratorInteraction?> CreateAsync(CeremonyTransaction transaction, CeremonyAccount account,
        string budgetSecret, string purpose, long destinationGeneration, string browserSecret, CeremonyPassword password)
    {
        account.RequireCurrent(transaction);
        Require(transaction, account.User.Id);
        if (!CeremonySecrets.IsIdentifier(budgetSecret) || !CeremonySecrets.IsIdentifier(browserSecret) || destinationGeneration <= 0 ||
            purpose is not (AdministratorPolicy.Enrollment or AdministratorPolicy.Login or AdministratorPolicy.Recovery or
                AdministratorPolicy.Replacement or AdministratorPolicy.Regeneration))
        {
            return null;
        }
        if (!clock.Observe(account.Authentication, out var now))
        {
            return null;
        }
        password.RequireCurrent(transaction, account, purpose, now);
        var budgetId = CeremonySecrets.Digest(budgetSecret);
        var budget = await database.Set<AdministratorBrowserBudget>().SingleOrDefaultAsync(row => row.Id == budgetId, transaction.Token);
        if (budget is not null && (!transaction.Plan.Browsers.Contains(budgetId, StringComparer.Ordinal) || budget.Id != budgetId))
        {
            return null;
        }
        if (!await HasInteractionCapacityAsync(transaction, account.User.Id, budgetId, budget is null, now))
        {
            CeremonyMetrics.Record(CeremonyOutcome.CapacityDenied);
            return null;
        }

        budget ??= new AdministratorBrowserBudget { Id = budgetId };
        budget.ExpiresAt = now + AdministratorPolicy.InteractionSeconds;
        if (database.Entry(budget).State == EntityState.Detached)
        {
            database.Add(budget);
        }
        var interaction = new AdministratorInteraction
        {
            PublicId = CeremonySecrets.Create(),
            UserId = account.User.Id,
            BrowserBudgetId = budgetId,
            BrowserDigest = CeremonySecrets.Digest(browserSecret),
            Purpose = purpose,
            Generation = account.Authentication.Generation,
            DestinationGeneration = destinationGeneration,
            SecurityStamp = account.User.SecurityStamp!,
            CreatedAt = now,
            PasswordVerifiedAt = password.VerifiedAt,
            ExpiresAt = now + AdministratorPolicy.InteractionSeconds
        };
        database.Add(interaction);
        await database.SaveChangesAsync(transaction.Token);
        return interaction;
    }

    /// <summary>Requires both live budgets and physical retained capacity, counting expired and consumed rows until deletion.</summary>
    private async Task<bool> HasInteractionCapacityAsync(CeremonyTransaction transaction, string userId, string budgetId, bool newBrowser, long now)
    {
        var rows = database.Set<AdministratorInteraction>();
        if (await rows.CountAsync(transaction.Token) >= AdministratorPolicy.RetainedRows ||
            (newBrowser && await database.Set<AdministratorBrowserBudget>().CountAsync(transaction.Token) >= AdministratorPolicy.RetainedRows))
        {
            return false;
        }
        var live = rows.Where(row => row.State == AdministratorPolicy.Pending && row.ExpiresAt > now);
        return await live.CountAsync(row => row.UserId == userId, transaction.Token) < AdministratorPolicy.LiveInteractions &&
            await live.CountAsync(row => row.BrowserBudgetId == budgetId, transaction.Token) < AdministratorPolicy.LiveInteractions;
    }

    /// <summary>Checks all session ceilings before callers consume a factor, recovery code or account authority.</summary>
    internal async Task<bool> HasSessionCapacityAsync(CeremonyTransaction transaction, string userId)
    {
        Require(transaction, userId);
        var now = clock.Now;
        var idle = now - AdministratorPolicy.IdleSeconds;
        var sessions = database.Set<AdministratorSession>();
        return await sessions.CountAsync(transaction.Token) < AdministratorPolicy.RetainedRows &&
            await sessions.CountAsync(row => row.UserId == userId, transaction.Token) < AdministratorPolicy.RetainedUserSessions &&
            await sessions.CountAsync(row => row.UserId == userId && !row.Revoked && row.ExpiresAt > now && row.LastActivityAt > idle, transaction.Token) < AdministratorPolicy.LiveSessions;
    }

    /// <summary>Rejects count-and-allocate operations that did not acquire admission before account serialization.</summary>
    private void Require(CeremonyTransaction transaction, string userId)
    {
        transaction.RequireContext(database);
        if (!transaction.Plan.Admission || !transaction.Plan.Users.Contains(userId, StringComparer.Ordinal))
        {
            throw new FactorUnavailableException();
        }
    }
}
