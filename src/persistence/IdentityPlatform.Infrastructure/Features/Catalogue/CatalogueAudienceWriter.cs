using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Stages bounded audience metadata in the caller's serialized transaction without independently committing or publishing.</summary>
internal static class CatalogueAudienceWriter
{
    /// <summary>Checks immutable resource identity and exact version before one write, retaining provider-native uniqueness and concurrency enforcement.</summary>
    internal static ValueTask<CatalogueAudience> StageAsync(ProtocolStorageScope scope, CatalogueIntent intent, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            if (scope.Owner.Transaction is null || intent.Resource != CatalogueResource.Audience || intent.Audience is not { } input)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            var rows = scope.Owner.Database.Set<ApiAudience>();
            ApiAudience row;
            if (intent.Action == CatalogueAction.Create)
            {
                if (await rows.AnyAsync(value => value.Resource == input.Resource, current))
                {
                    throw new CatalogueRejected(CatalogueFailure.Duplicate);
                }
                row = new ApiAudience { Id = Guid.NewGuid().ToString(), Resource = input.Resource };
                rows.Add(row);
            }
            else
            {
                row = await rows.SingleOrDefaultAsync(value => value.Id == intent.Id, current)
                    ?? throw new CatalogueRejected(CatalogueFailure.Missing);
                var before = CatalogueAudience.Capture(row);
                if (row.Id != intent.Id || !CatalogueVersion.Matches(intent.Version, CatalogueVersion.Audience, row.Id, row.Revision))
                {
                    throw new CatalogueRejected(CatalogueFailure.Stale);
                }
                if (before.Resource != input.Resource)
                {
                    throw new CatalogueRejected(CatalogueFailure.Immutable);
                }
                row.Revision = checked(row.Revision + 1);
            }
            row.DisplayName = input.DisplayName;
            row.Owner = input.Owner;
            row.Enabled = input.Enabled;
            var expected = CatalogueAudience.Capture(row);
            await scope.Owner.Database.SaveChangesAsync(current);
            return expected;
        }, token, write: true);
}
