using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Projects bounded registration metadata without materializing native hashes, keys or arbitrary properties.</summary>
internal static class ApplicationDetailsReader
{
    private const int Probe = 1;
    private const int NativeJsonBytes = 131072;
    private const int NativeNamesLength = 4096;
    private const string EmptyObject = "{}";
    private const string EmptyArray = "[]";
    private const string PilotClient = "identityplatform-pilot";

    /// <summary>Checks both native and policy IDs ordinally after provider lookup and never adopts native or system registrations.</summary>
    internal static async Task<(ApplicationDetails? Details, string? Version)> ReadAsync(PlatformDbContext database, string id, DateTimeOffset expiry, CancellationToken token)
    {
        var row = await Rows(database, id).SingleOrDefaultAsync(token);
        if (row is null || row.Id != id)
        {
            return (null, null);
        }
        if (!ApplicationProjection.IsValid(new(row.Id, row.ClientId, row.DisplayName, row.ClientType)))
        {
            throw new InvalidDataException();
        }
        var policy = await database.Set<ApplicationPolicy>().AsNoTracking().SingleOrDefaultAsync(value => value.ApplicationId == id, token);
        if (policy is not null && policy.ApplicationId != id)
        {
            throw new InvalidDataException();
        }
        if (policy is null || await SystemAsync(database, id, row.ClientId, token))
        {
            return (new(row.Id, row.ClientId, row.DisplayName, [], null, false, null, expiry), null);
        }
        if (policy.Profile != ApplicationEditorRules.Profile || policy.Revision < 1 || policy.CredentialRevision < 1 ||
            policy.CredentialIssuedAt < 0 || policy.CredentialExpiresAt <= policy.CredentialIssuedAt || policy.ObservedAt < policy.CredentialIssuedAt ||
            (!row.NativeProfile || row.ApplicationType != OpenIddictConstants.ApplicationTypes.Web || row.ClientType != OpenIddictConstants.ClientTypes.Confidential ||
            row.ConsentType != OpenIddictConstants.ConsentTypes.Explicit) || row.Redirects is null || row.Redirects.Length > NativeJsonBytes ||
            row.Permissions is null || row.Permissions.Length > NativeNamesLength || row.Requirements is null || row.Requirements.Length > NativeNamesLength)
        {
            throw new InvalidDataException();
        }
        var descriptor = Descriptor(row);
        if (!ManagedApplicationProfile.Matches(descriptor))
        {
            throw new InvalidDataException();
        }
        return (new(row.Id, row.ClientId, row.DisplayName, descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToArray(),
            policy.Profile, true, new(DateTimeOffset.FromUnixTimeSeconds(policy.CredentialIssuedAt), DateTimeOffset.FromUnixTimeSeconds(policy.CredentialExpiresAt)), expiry),
            ApplicationVersion.Create(row.Id, policy.Revision));
    }

    /// <summary>Limits database output before materialization and computes profile flags without retrieving protected native columns.</summary>
    private static IQueryable<Row> Rows(PlatformDbContext database, string id) => database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().Where(row => row.Id == id).Select(row => new Row(
        row.Id!.Substring(0, ApplicationProjection.IdentifierLength + Probe),
        row.ClientId!.Substring(0, ApplicationProjection.ClientIdentifierLength + Probe),
        row.DisplayName == null ? null : row.DisplayName.Substring(0, ApplicationProjection.DisplayNameLength + Probe),
        row.ClientType == null ? null : row.ClientType.Substring(0, ApplicationProjection.ClientTypeLength + Probe),
        row.RedirectUris == null ? null : row.RedirectUris.Substring(0, NativeJsonBytes + Probe),
        row.Permissions == null ? null : row.Permissions.Substring(0, NativeNamesLength + Probe),
        row.Requirements == null ? null : row.Requirements.Substring(0, NativeNamesLength + Probe),
        row.ApplicationType == null ? null : row.ApplicationType.Substring(0, ApplicationProjection.ClientTypeLength + Probe),
        row.ConsentType == null ? null : row.ConsentType.Substring(0, ApplicationProjection.ClientTypeLength + Probe),
        row.ClientSecret != null && row.ClientSecret != string.Empty &&
        row.JsonWebKeySet == null && (row.PostLogoutRedirectUris == null || row.PostLogoutRedirectUris == EmptyArray) &&
        (row.Properties == null || row.Properties == EmptyObject) && (row.Settings == null || row.Settings == EmptyObject) &&
        (row.DisplayNames == null || row.DisplayNames == EmptyObject)));

    /// <summary>Reconstructs only finite public profile fields and rejects duplicate native array entries or invalid callback text.</summary>
    private static OpenIddictApplicationDescriptor Descriptor(Row row)
    {
        var redirects = JsonSerializer.Deserialize<string[]>(row.Redirects!, ApplicationJson.Options) ?? throw new InvalidDataException();
        var permissions = JsonSerializer.Deserialize<string[]>(row.Permissions!, ApplicationJson.Options) ?? throw new InvalidDataException();
        var requirements = JsonSerializer.Deserialize<string[]>(row.Requirements!, ApplicationJson.Options) ?? throw new InvalidDataException();
        if (redirects.Length is < 1 or > ApplicationEditorRules.MaximumRedirects || redirects.Any(value => !ApplicationEditorRules.IsRedirect(value)) ||
            redirects.Distinct(StringComparer.Ordinal).Count() != redirects.Length || permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length ||
            requirements.Distinct(StringComparer.Ordinal).Count() != requirements.Length)
        {
            throw new InvalidDataException();
        }
        var result = new OpenIddictApplicationDescriptor
        {
            ClientId = row.ClientId,
            DisplayName = row.DisplayName,
            ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit
        };
        result.RedirectUris.UnionWith(redirects.Select(value => new Uri(value)));
        result.Permissions.UnionWith(permissions);
        result.Requirements.UnionWith(requirements);
        return result;
    }

    /// <summary>Protects reserved names and persisted administrator/pilot bindings independently of native profile drift.</summary>
    private static async Task<bool> SystemAsync(PlatformDbContext database, string id, string clientId, CancellationToken token) =>
        string.Equals(clientId, AdministratorProtocolIdentity.ClientId, StringComparison.OrdinalIgnoreCase) || string.Equals(clientId, PilotClient, StringComparison.OrdinalIgnoreCase) ||
        await database.Set<AdministratorProtocolProfile>().AsNoTracking().AnyAsync(row => row.ApplicationId == id, token) ||
        await database.Set<PilotManifest>().AsNoTracking().AnyAsync(row => row.ApplicationId == id, token);

    /// <summary>Contains only bounded presentation/profile metadata; no native credential or arbitrary object is retained.</summary>
    private sealed record Row(string Id, string ClientId, string? DisplayName, string? ClientType, string? Redirects, string? Permissions, string? Requirements, string? ApplicationType, string? ConsentType, bool NativeProfile);
}
