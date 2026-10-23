using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol.Upgrade;

/// <summary>Revokes only canonical pending native request rows of the exact old PAR-only fixed profile.</summary>
internal static class AdministratorPendingRequests
{
    /// <summary>Bounds and validates metadata before native bulk revocation, avoiding provider collation-dependent partial invalidation.</summary>
    internal static async ValueTask RevokeAsync(ProtocolStorageScope scope, string application, CancellationToken token)
    {
        var expected = await CountAsync(scope.Owner, application, token);
        var actual = await scope.Services.GetRequiredService<IOpenIddictTokenManager>().RevokeAsync(subject: null, client: application,
            status: OpenIddictConstants.Statuses.Valid, type: OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, cancellationToken: token);
        AdministratorRegistrationUpgrade.Require(actual == expected && await CountAsync(scope.Owner, application, token) == 0);
    }

    /// <summary>Reads bounded status/type metadata only; no protected payload, subject or reference enters an audit or diagnostic.</summary>
    private static ValueTask<int> CountAsync(ProtocolStorageOwner owner, string application, CancellationToken token) => owner.RunAsync(async current =>
    {
        var rows = await owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Application!.Id == application)
            .Select(row => new { row.Type, row.Status }).Take(AdministratorTokenBudget.TokenLimit + 1).ToArrayAsync(current);
        AdministratorRegistrationUpgrade.Require(rows.Length <= AdministratorTokenBudget.TokenLimit);
        var requests = rows.Where(row => string.Equals(row.Type, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, StringComparison.OrdinalIgnoreCase)).ToArray();
        AdministratorRegistrationUpgrade.Require(requests.Length <= AdministratorTokenBudget.RequestLimit && requests.All(row =>
            row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken && row.Status is OpenIddictConstants.Statuses.Valid or
                OpenIddictConstants.Statuses.Redeemed or OpenIddictConstants.Statuses.Revoked or OpenIddictConstants.Statuses.Rejected));
        return requests.Count(row => row.Status == OpenIddictConstants.Statuses.Valid);
    }, token);
}
