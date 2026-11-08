using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Freezes audience metadata for exact post-save verification; a snapshot carries no independent authority to commit.</summary>
internal sealed record CatalogueAudience(string Id, string Resource, string DisplayName, string Owner, bool Enabled, long Revision)
{
    internal string Version => CatalogueVersion.Create(CatalogueVersion.Audience, Id, Revision);
    /// <summary>Rejects aliased, malformed or nonpositive persisted identities before exposing them as protocol authority.</summary>
    internal static CatalogueAudience Capture(ApiAudience row)
    {
        if (!CatalogueRules.IsIdentifier(row.Id) || row.Revision <= 0 ||
            CatalogueRules.Validate(new AudienceInput(row.Resource, row.DisplayName, row.Owner, row.Enabled)).Count != 0)
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        return new(row.Id, row.Resource, row.DisplayName, row.Owner, row.Enabled, row.Revision);
    }

    /// <summary>Reads fresh product state under the owner's original cancellation and rejects database collation aliases.</summary>
    internal static async ValueTask<CatalogueAudience> ReadAsync(ProtocolStorageScope scope, string id, CancellationToken token)
    {
        var row = await scope.Owner.RunAsync(current => new ValueTask<ApiAudience?>(scope.Owner.Database.Set<ApiAudience>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, current)), token);
        return row is not null && row.Id == id ? Capture(row) : throw new CatalogueRejected(CatalogueFailure.Missing);
    }

    /// <summary>Requires actual persisted equality after manager/audit saves, detecting suppressed or altered writes.</summary>
    internal async ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (this != await ReadAsync(scope, Id, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Creates a fixed safe response only after the owning operation has separately established current reference state.</summary>
    internal AudienceDetails Details(bool referenced, DateTimeOffset expiry) => new(Id, Resource, DisplayName, Owner, Enabled,
        Version, referenced, expiry);
}
