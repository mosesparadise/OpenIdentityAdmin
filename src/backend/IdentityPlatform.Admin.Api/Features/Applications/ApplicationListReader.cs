using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Reads finite native summaries in the selected provider's ordinal ordering without materializing credential-bearing entities.</summary>
internal static class ApplicationListReader
{
    private const int OverflowProbe = 1;
    private const string InvalidMetadata = "Application metadata is outside the supported projection.";

    /// <summary>Projects bounded SQL prefixes, rejects overflow and returns a single-query lookahead page; no native state is changed.</summary>
    internal static async Task<ApplicationPage> ReadAsync(PlatformDbContext database, ApplicationListQuery query, DateTimeOffset expiresAt, CancellationToken token)
    {
        var ordinal = database.GetDatabaseProvider().Model.SessionOrdinalCollation;
        var source = database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking();
        if (query.Search.Length != 0)
        {
            source = source.Where(row => EF.Functions.Collate(row.ClientId!, ordinal).StartsWith(query.Search));
        }
        var rows = await source.OrderBy(row => EF.Functions.Collate(row.ClientId!, ordinal))
            .ThenBy(row => EF.Functions.Collate(row.Id!, ordinal))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + OverflowProbe)
            .Select(row => new ApplicationSummary(
                row.Id!.Substring(0, ApplicationProjection.IdentifierLength + OverflowProbe),
                row.ClientId!.Substring(0, ApplicationProjection.ClientIdentifierLength + OverflowProbe),
                row.DisplayName == null ? null : row.DisplayName.Substring(0, ApplicationProjection.DisplayNameLength + OverflowProbe),
                row.ClientType == null ? null : row.ClientType.Substring(0, ApplicationProjection.ClientTypeLength + OverflowProbe)))
            .ToArrayAsync(token);
        if (rows.Any(row => !ApplicationProjection.IsValid(row)))
        {
            throw new InvalidOperationException(InvalidMetadata);
        }
        return new(rows.Take(query.PageSize).ToArray(), query.Page, query.PageSize, rows.Length > query.PageSize, expiresAt);
    }
}
