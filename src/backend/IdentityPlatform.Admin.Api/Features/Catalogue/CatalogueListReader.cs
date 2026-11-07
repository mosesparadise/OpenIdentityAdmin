using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Pages only product-managed catalogue rows using provider ordinal ordering and bounded SQL projections.</summary>
internal static class CatalogueListReader
{
    private const int Probe = 1;

    /// <summary>Reuses the established finite management query grammar and applies independent scope/resource bounds before publication.</summary>
    internal static async Task<CataloguePage> ReadAsync(PlatformDbContext database, CatalogueResource kind,
        ApplicationListQuery query, DateTimeOffset expiry, CancellationToken token)
    {
        var ordinal = database.GetDatabaseProvider().Model.SessionOrdinalCollation;
        var source = Rows(database, kind, ordinal);
        if (query.Search.Length != 0)
        {
            source = source.Where(row => EF.Functions.Collate(row.Name, ordinal).StartsWith(query.Search));
        }
        var rows = await source.OrderBy(row => EF.Functions.Collate(row.Name, ordinal)).ThenBy(row => EF.Functions.Collate(row.Id, ordinal))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + Probe)
            .Select(row => new CatalogueSummary(row.Id.Substring(0, CatalogueRules.IdentifierLength + Probe),
                row.Name.Substring(0, CatalogueRules.ResourceLength + Probe), row.DisplayName.Substring(0, CatalogueRules.DisplayLength + Probe), row.Enabled)).ToArrayAsync(token);
        if (rows.Any(row => !CatalogueRules.IsIdentifier(row.Id) ||
            !(kind == CatalogueResource.Scope ? CatalogueRules.IsName(row.Name) : CatalogueRules.IsResource(row.Name)) ||
            !ApplicationEditorRules.IsText(row.DisplayName, CatalogueRules.DisplayLength)))
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        return new(rows.Take(query.PageSize).ToArray(), query.Page, query.PageSize, rows.Length > query.PageSize, expiry);
    }

    /// <summary>Reads native scope identity joined to explicit policy; unknown kinds cannot select another catalogue accidentally.</summary>
    private static IQueryable<Row> Rows(PlatformDbContext database, CatalogueResource kind, string ordinal) => kind switch
    {
        CatalogueResource.Scope => from native in database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking()
                                   join policy in database.Set<ScopePolicy>().AsNoTracking() on EF.Functions.Collate(native.Id!, ordinal) equals EF.Functions.Collate(policy.ScopeId, ordinal)
                                   where !database.Set<AdministratorProtocolProfile>().Any(row => EF.Functions.Collate(row.ScopeId, ordinal) == EF.Functions.Collate(native.Id!, ordinal)) &&
                                         !database.Set<PilotManifest>().Any(row => EF.Functions.Collate(row.ScopeId, ordinal) == EF.Functions.Collate(native.Id!, ordinal))
                                   select new Row { Id = native.Id!, Name = native.Name!, DisplayName = native.DisplayName!, Enabled = policy.Enabled },
        CatalogueResource.Audience => database.Set<ApiAudience>().AsNoTracking().Select(row => new Row { Id = row.Id, Name = row.Resource, DisplayName = row.DisplayName, Enabled = row.Enabled }),
        _ => throw new InvalidOperationException()
    };
    /// <summary>Uses member initializers for provider translation; only the final bounded projection is materialized.</summary>
    private sealed class Row
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool Enabled
        {
            get; init;
        }
    }

}
