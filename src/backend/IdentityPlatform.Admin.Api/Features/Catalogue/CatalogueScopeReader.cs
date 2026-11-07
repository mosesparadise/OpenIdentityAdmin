using System.Text.Json;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Reads finite native scope metadata through the selected rollback-only store without materializing arbitrary native properties.</summary>
internal static class CatalogueScopeReader
{
    private const int Probe = 1;
    private const int EmptyObjectProbe = 3;
    private const int ResourceJsonLength = 2048;
    private const int JsonDepth = 2;
    private const string EmptyObject = "{}";

    /// <summary>Protects persisted system IDs and requires exact native/policy/resource identity before sharing the pure catalogue validator.</summary>
    internal static async Task<CatalogueScope> ReadAsync(IServiceProvider services, PlatformDbContext database, string id, CancellationToken token)
    {
        if (await database.Set<AdministratorProtocolProfile>().AsNoTracking().AnyAsync(row => row.ScopeId == id, token) ||
            await database.Set<PilotManifest>().AsNoTracking().AnyAsync(row => row.ScopeId == id, token))
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        var row = await NativeAsync(services, id, token);
        if (row is null || row.Id != id)
        {
            throw new CatalogueRejected(CatalogueFailure.Missing);
        }
        var descriptor = Descriptor(row);
        var policy = await database.Set<ScopePolicy>().AsNoTracking().SingleOrDefaultAsync(value => value.ScopeId == id, token);
        var resource = descriptor.Resources.SingleOrDefault();
        var audience = resource is null ? null : await database.Set<ApiAudience>().AsNoTracking().SingleOrDefaultAsync(value => value.Resource == resource, token);
        if (resource is not null && (audience is null || audience.Resource != resource))
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        return CatalogueScope.Capture(id, descriptor, policy, audience is null ? null : CatalogueAudience.Capture(audience));
    }

    /// <summary>Bounds every projected native string in SQL and tests excluded metadata without retrieving its contents.</summary>
    private static async Task<Row?> NativeAsync(IServiceProvider services, string id, CancellationToken token)
    {
        var store = services.GetRequiredService<IOpenIddictScopeStore<OpenIddictEntityFrameworkCoreScope>>();
        var rows = store.ListAsync((source, key) => source.Where(row => row.Id == key).Select(row => new Row(
            row.Id!.Substring(0, CatalogueRules.IdentifierLength + Probe),
            row.Name == null ? null : row.Name.Substring(0, CatalogueRules.NameLength + Probe),
            row.DisplayName == null ? null : row.DisplayName.Substring(0, CatalogueRules.DisplayLength + Probe),
            row.Resources == null ? null : row.Resources.Substring(0, ResourceJsonLength + Probe),
            row.Description == null,
            row.Descriptions == null ? null : row.Descriptions.Substring(0, EmptyObjectProbe),
            row.DisplayNames == null ? null : row.DisplayNames.Substring(0, EmptyObjectProbe),
            row.Properties == null ? null : row.Properties.Substring(0, EmptyObjectProbe))), id, token);
        Row? result = null;
        await foreach (var row in rows.WithCancellation(token))
        {
            if (result is not null)
            {
                throw new InvalidDataException();
            }
            result = row;
        }
        return result;
    }

    /// <summary>Rejects malformed, duplicate or excess native resources before constructing a fixed descriptor with no hidden metadata.</summary>
    private static OpenIddictScopeDescriptor Descriptor(Row row)
    {
        if (!row.NoDescription || row.Descriptions is not (null or EmptyObject) || row.DisplayNames is not (null or EmptyObject) ||
            row.Properties is not (null or EmptyObject) || row.Resources?.Length > ResourceJsonLength)
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        var descriptor = new OpenIddictScopeDescriptor { Name = row.Name, DisplayName = row.DisplayName };
        if (row.Resources is null)
        {
            return descriptor;
        }
        using var json = JsonDocument.Parse(row.Resources, new JsonDocumentOptions { MaxDepth = JsonDepth });
        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > 1)
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        foreach (var value in json.RootElement.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String || !CatalogueRules.IsResource(value.GetString()))
            {
                throw new CatalogueRejected(CatalogueFailure.Unsupported);
            }
            descriptor.Resources.Add(value.GetString()!);
        }
        return descriptor;
    }

    /// <summary>Contains only bounded public native metadata and a flag for excluded fields.</summary>
    private sealed record Row(string Id, string? Name, string? DisplayName, string? Resources, bool NoDescription,
        string? Descriptions, string? DisplayNames, string? Properties);
}
