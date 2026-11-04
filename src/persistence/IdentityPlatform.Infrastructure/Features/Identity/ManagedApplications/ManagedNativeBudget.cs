using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Counts actual artifacts for all policy-owned managed applications under common governance serialization.</summary>
internal readonly record struct ManagedNativeBudget(long Tokens, long Authorizations, long Requests, long LiveRequests)
{
    /// <summary>Reserves precisely one cached request, never inferring native capacity from interaction rows.</summary>
    internal bool CanCache => Tokens >= 0 && Tokens < ManagedLoginRules.TokenLimit && Authorizations >= 0 &&
        Authorizations <= ManagedLoginRules.AuthorizationLimit && Requests >= 0 && Requests < ManagedLoginRules.RequestLimit &&
        LiveRequests >= 0 && LiveRequests < ManagedLoginRules.LiveLimit;

    /// <summary>Rejects partial or additional native artifacts after the actual cache generator has returned.</summary>
    internal bool MatchesCache(ManagedNativeBudget current) => CanCache && current.Tokens == Tokens + 1 &&
        current.Authorizations == Authorizations && current.Requests == Requests + 1 && current.LiveRequests == LiveRequests + 1;

    /// <summary>Uses bounded native metadata and counts every status/type, including malformed or orphaned managed artifacts.</summary>
    internal static ValueTask<ManagedNativeBudget> ReadAsync(ProtocolStorageScope scope, long expirationCutoff, CancellationToken token) =>
        scope.Owner.RunAsync<ManagedNativeBudget>(async current =>
        {
            var database = scope.Owner.Database;
            var policies = database.Set<ApplicationPolicy>();
            var collation = database.GetDatabaseProvider().Model.IdentityOrdinalCollation;
            var tokens = database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
                .Where(row => policies.Any(policy => policy.ApplicationId == (collation == null ? row.Application!.Id : EF.Functions.Collate(row.Application!.Id!, collation))));
            var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
                .LongCountAsync(row => policies.Any(policy => policy.ApplicationId == (collation == null ? row.Application!.Id : EF.Functions.Collate(row.Application!.Id!, collation))), current);
            var count = await tokens.LongCountAsync(current);
            if (count > ManagedLoginRules.TokenLimit)
            {
                return new(count, authorizations, ManagedLoginRules.RequestLimit, ManagedLoginRules.LiveLimit);
            }
            var rows = await tokens.Select(row => new { row.Type, row.Status, row.ExpirationDate })
                .Take(ManagedLoginRules.TokenLimit + 1).ToArrayAsync(current);
            if (rows.LongLength != count)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            var requests = rows.Where(row => string.Equals(row.Type, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken,
                StringComparison.OrdinalIgnoreCase)).ToArray();
            var cutoff = DateTimeOffset.FromUnixTimeSeconds(expirationCutoff).UtcDateTime;
            return new(count, authorizations, requests.LongLength, requests.LongCount(row => !Terminal(row.Status) &&
                (row.ExpirationDate is null || row.ExpirationDate >= cutoff)));
        }, token);

    /// <summary>Unknown status remains conservatively live independently of provider collation.</summary>
    private static bool Terminal(string? status) => string.Equals(status, OpenIddictConstants.Statuses.Redeemed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OpenIddictConstants.Statuses.Revoked, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OpenIddictConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase);
}
