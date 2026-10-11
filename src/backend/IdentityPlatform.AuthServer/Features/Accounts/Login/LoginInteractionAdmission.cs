using System.Data;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public sealed class LoginInteractionAdmission(PlatformDbContext database, TimeProvider time)
{
    public async Task<LoginInteraction?> CreateAsync(HttpContext context, string tokenId, string requestUri, long nativeExpiry)
    {
        var candidates = context.Request.Cookies.Keys.Where(name => name.StartsWith(PilotProfile.CorrelationPrefix, StringComparison.Ordinal))
            .Take(PilotProfile.MaximumInteractions + 1).ToArray();
        if (candidates.Length > PilotProfile.MaximumInteractions || tokenId.Length is < 1 or > 100 ||
            requestUri.Length is < 1 or > 512 || nativeExpiry <= Now)
        {
            return null;
        }
        var credential = context.Request.Cookies[PilotProfile.BudgetCookie];
        if (credential is not null && !InteractionSecrets.IsIdentifier(credential))
        {
            DeleteBudgetCookie(context);
            return null;
        }
        var owned = await PresentedAsync(context, candidates);
        var budgetId = credential is null ? null : InteractionSecrets.Digest(credential);
        if (budgetId is null && owned.Count != 0)
        {
            return null;
        }

        var cancellationToken = context.RequestAborted;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        LoginBrowserBudget budget;
        if (budgetId is null)
        {
            credential = InteractionSecrets.Create();
            budget = new LoginBrowserBudget { Id = InteractionSecrets.Digest(credential) };
            database.Add(budget);
        }
        else
        {
            // Hold the database write lock through commit. Serializing a count of the
            // request's stale cookie snapshot would still allow more than eight rows.
            var locked = await database.Set<LoginBrowserBudget>().Where(value => value.Id == budgetId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.LastAdmissionAt, Now), cancellationToken);
            if (locked != 1)
            {
                DeleteBudgetCookie(context);
                return null;
            }
            budget = await database.Set<LoginBrowserBudget>().AsNoTracking().SingleAsync(value => value.Id == budgetId, cancellationToken);
        }
        var active = await database.Set<LoginInteraction>()
            .Where(value => value.BudgetId == budget.Id && !value.Consumed && value.ExpiresAt > Now)
            .Take(PilotProfile.MaximumInteractions).CountAsync(cancellationToken);
        // Recheck after database waits, before extending either lifetime. An older count
        // can conservatively deny capacity that expired while the query was running.
        var now = Now;
        if (budgetId is not null && budget.ExpiresAt <= now)
        {
            DeleteBudgetCookie(context);
            return null;
        }
        if (owned.Any(value => value.BudgetId != budget.Id) || nativeExpiry <= now || active >= PilotProfile.MaximumInteractions)
        {
            return null;
        }
        var secret = InteractionSecrets.Create();
        var interaction = new LoginInteraction
        {
            Id = InteractionSecrets.Create(),
            RequestTokenId = tokenId,
            RequestUri = requestUri,
            ClientId = PilotProfile.ClientId,
            BrowserDigest = InteractionSecrets.Digest(secret),
            BudgetId = budget.Id,
            CreatedAt = now,
            ExpiresAt = Math.Min(now + PilotProfile.InteractionSeconds, nativeExpiry)
        };
        budget.LastAdmissionAt = now;
        budget.ExpiresAt = now + PilotProfile.InteractionSeconds;
        if (budgetId is not null)
        {
            await database.Set<LoginBrowserBudget>().Where(value => value.Id == budgetId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.LastAdmissionAt, now)
                    .SetProperty(value => value.ExpiresAt, budget.ExpiresAt), cancellationToken);
        }
        database.Add(interaction);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        context.Response.Cookies.Append(PilotProfile.BudgetCookie, credential!, LoginInteractions.CookieOptions());
        context.Response.Cookies.Append(PilotProfile.CorrelationPrefix + interaction.Id, secret, LoginInteractions.CookieOptions());
        return interaction;
    }

    private async Task<List<LoginInteraction>> PresentedAsync(HttpContext context, string[] candidates)
    {
        var ids = candidates.Select(name => name[PilotProfile.CorrelationPrefix.Length..]).Where(InteractionSecrets.IsIdentifier).ToArray();
        var active = await database.Set<LoginInteraction>().AsNoTracking()
            .Where(value => ids.Contains(value.Id) && !value.Consumed && value.ExpiresAt > Now)
            .ToDictionaryAsync(value => value.Id, context.RequestAborted);
        var owned = new List<LoginInteraction>();
        foreach (var cookie in candidates)
        {
            var id = cookie[PilotProfile.CorrelationPrefix.Length..];
            if (active.TryGetValue(id, out var interaction) && InteractionSecrets.Matches(context.Request.Cookies[cookie], interaction.BrowserDigest))
            {
                owned.Add(interaction);
            }
            else
            {
                context.Response.Cookies.Delete(cookie, LoginInteractions.CookieOptions());
            }
        }
        return owned;
    }

    private static void DeleteBudgetCookie(HttpContext context) => context.Response.Cookies.Delete(PilotProfile.BudgetCookie, LoginInteractions.CookieOptions());

    private long Now => time.GetUtcNow().ToUnixTimeSeconds();
}
