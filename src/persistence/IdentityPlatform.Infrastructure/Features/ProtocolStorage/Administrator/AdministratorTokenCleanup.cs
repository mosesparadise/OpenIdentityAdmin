using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Reclaims at most one shared batch of old fixed-profile native rows without global pruning, retries or authorization cascades.</summary>
internal static class AdministratorTokenCleanup
{
    /// <summary>Deletes only finite expired tokens and then empty ad-hoc authorizations, rechecking every eligibility predicate in each delete.</summary>
    internal static async Task RunAsync(DbContext database, string application, DateTime retention, DateTime expiration, CancellationToken token)
    {
        var tokens = database.Set<OpenIddictEntityFrameworkCoreToken>();
        var expired = tokens.Where(row => row.Application!.Id == application && row.CreationDate < retention && row.ExpirationDate < expiration);
        var identifiers = await expired.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).Take(AdministratorTokenBudget.CleanupLimit).ToArrayAsync(token);
        var deleted = identifiers.Length == 0 ? 0 : await expired.Where(row => identifiers.Contains(row.Id)).ExecuteDeleteAsync(token);
        var remaining = AdministratorTokenBudget.CleanupLimit - deleted;
        if (remaining <= 0)
        {
            return;
        }
        var empty = database.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(row => row.Application!.Id == application &&
            row.Type == OpenIddictConstants.AuthorizationTypes.AdHoc && row.CreationDate < retention &&
            !tokens.Any(child => child.Authorization!.Id == row.Id));
        var authorizations = await empty.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).Take(remaining).ToArrayAsync(token);
        if (authorizations.Length != 0)
        {
            await empty.Where(row => authorizations.Contains(row.Id)).ExecuteDeleteAsync(token);
        }
    }
}
