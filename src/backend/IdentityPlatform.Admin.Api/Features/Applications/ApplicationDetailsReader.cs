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
    private const int MaximumKeySetLength = 16384;
    private const int NativeNamesLength = 4096;
    private const string EmptyObject = "{}";
    private const string EmptyArray = "[]";
    private const string PilotClient = "identityplatform-pilot";

    /// <summary>Checks both native and policy IDs ordinally after provider lookup and never adopts native or system registrations.</summary>
    internal static async Task<(ApplicationDetails? Details, string? Version)> ReadAsync(PlatformDbContext database, string id, DateTimeOffset expiry, CancellationToken token)
    {
        var state = await ReadStateAsync(database, id, expiry, token);
        return (state.Details, state.Version);
    }

    /// <summary>Returns one bounded validated native snapshot for both ordinary details and the additive scope-assignment projection.</summary>
    internal static async Task<ApplicationDetailsSnapshot> ReadStateAsync(PlatformDbContext database, string id, DateTimeOffset expiry, CancellationToken token)
    {
        var row = await Rows(database, id).SingleOrDefaultAsync(token);
        if (row is null || row.Id != id)
        {
            return new(null, null, null);
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
            return new(new(row.Id, row.ClientId, row.DisplayName, [], null, false, null, expiry), null, null);
        }
        var machine = policy.Profile is MachineClientRules.Profile or ApplicationKeyApproval.MachineProfile;
        if (policy.Revision < 1 || policy.ObservedAt < 0 || !row.NativeProfile || row.Redirects?.Length > NativeJsonBytes || row.Redirects is null && !machine ||
            row.Permissions is null || row.Permissions.Length > NativeNamesLength || row.Requirements?.Length > NativeNamesLength || row.Requirements is null && !machine)
        {
            throw new InvalidDataException();
        }
        var descriptor = Descriptor(row);
        string[]? origins = null;
        if (policy.Profile == ApplicationEditorRules.BrowserProfile)
        {
            origins = await database.Set<ApplicationBrowserOrigin>().AsNoTracking().Where(value => value.ApplicationId == id)
                .OrderBy(value => value.Slot).Select(value => value.Origin).Take(BrowserOriginPolicy.MaximumOrigins + 1).ToArrayAsync(token);
            if (!BrowserOriginPolicy.IsValid(origins))
            {
                throw new InvalidDataException();
            }
        }
        var keyed = policy.Profile is ApplicationKeyApproval.MachineProfile or ApplicationKeyApproval.WebProfile;
        if (keyed != row.HasKey)
        {
            throw new InvalidDataException();
        }
        if (keyed)
        {
            await RequirePublicKeyAsync(database, policy, descriptor, token);
        }
        var credential = Credential(policy, row, descriptor, keyed);
        var retirement = await ApplicationRetirementReader.ReadAsync(database, policy, token);
        return new(new(row.Id, row.ClientId, row.DisplayName, descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToArray(),
            policy.Profile, !policy.Retired, credential, expiry, policy.Enabled,
            policy.Retired, retirement, origins),
            ApplicationVersion.Create(row.Id, policy.Revision), descriptor.Permissions
                .Where(value => value.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal))
                .Select(value => value[OpenIddictConstants.Permissions.Prefixes.Scope.Length..]).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Reads a bounded native key set solely for an explicitly approved managed identity and validates it without exposing it in the response.</summary>
    private static async Task RequirePublicKeyAsync(PlatformDbContext database, ApplicationPolicy policy, OpenIddictApplicationDescriptor descriptor, CancellationToken token)
    {
        var approval = await database.Set<ApplicationKeyApproval>().AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == policy.ApplicationId, token);
        if (approval?.ApplicationId != policy.ApplicationId || approval.CredentialRevision != policy.CredentialRevision)
        {
            throw new InvalidDataException();
        }
        var json = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().Where(row => row.Id == policy.ApplicationId)
            .Select(row => row.JsonWebKeySet!.Substring(0, MaximumKeySetLength + Probe)).SingleAsync(token);
        if (json is null || json.Length > MaximumKeySetLength)
        {
            throw new InvalidDataException();
        }
        descriptor.JsonWebKeySet = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(json);
        if (AdministratorPublicKey.FromNative(descriptor.JsonWebKeySet) is not { } key || key.Fingerprint != approval.Fingerprint ||
            ApplicationKeyApproval.Material(key) != approval.Thumbprint)
        {
            throw new InvalidDataException();
        }
    }

    /// <summary>Requires matching profile, credential kind and native capabilities without retrieving a stored secret.</summary>
    private static ApplicationCredentialMetadata? Credential(ApplicationPolicy policy, Row row, OpenIddictApplicationDescriptor descriptor, bool keyed)
    {
        if (policy.Profile is ApplicationEditorRules.NativeProfile or ApplicationEditorRules.BrowserProfile && row.NoSecret && ManagedApplicationProfile.MatchesProfile(policy.Profile, descriptor) &&
            policy.CredentialRevision == 0 && policy.CredentialIssuedAt is null && policy.CredentialExpiresAt is null)
        {
            return null;
        }
        if ((keyed && row.NoSecret && ManagedApplicationProfile.MatchesKeyProfile(policy.Profile, descriptor) || row.HasSecret && (policy.Profile == ApplicationEditorRules.Profile && ManagedApplicationProfile.MatchesManagement(descriptor) ||
            policy.Profile == MachineClientRules.Profile && ManagedApplicationProfile.MatchesMachine(descriptor))) &&
            policy.CredentialRevision >= 1 && policy.CredentialIssuedAt is { } issued && policy.CredentialExpiresAt is { } expires &&
            issued >= 0 && expires > issued && policy.ObservedAt >= issued)
        {
            return new(DateTimeOffset.FromUnixTimeSeconds(issued), DateTimeOffset.FromUnixTimeSeconds(expires));
        }
        throw new InvalidDataException();
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
        row.ClientSecret == null, row.ClientSecret != null && row.ClientSecret != string.Empty,
        row.JsonWebKeySet != null, (row.PostLogoutRedirectUris == null || row.PostLogoutRedirectUris == EmptyArray) &&
        (row.Properties == null || row.Properties == EmptyObject) && (row.Settings == null || row.Settings == EmptyObject) &&
        (row.DisplayNames == null || row.DisplayNames == EmptyObject)));

    /// <summary>Reconstructs only finite public profile fields and rejects duplicate native array entries or invalid callback text.</summary>
    private static OpenIddictApplicationDescriptor Descriptor(Row row)
    {
        var redirects = JsonSerializer.Deserialize<string[]>(row.Redirects ?? EmptyArray, ApplicationJson.Options) ?? throw new InvalidDataException();
        var permissions = JsonSerializer.Deserialize<string[]>(row.Permissions!, ApplicationJson.Options) ?? throw new InvalidDataException();
        var requirements = JsonSerializer.Deserialize<string[]>(row.Requirements ?? EmptyArray, ApplicationJson.Options) ?? throw new InvalidDataException();
        if (redirects.Length > ApplicationEditorRules.MaximumRedirects || redirects.Any(value => !ApplicationEditorRules.IsRedirect(value)) ||
            redirects.Distinct(StringComparer.Ordinal).Count() != redirects.Length || permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length ||
            requirements.Distinct(StringComparer.Ordinal).Count() != requirements.Length)
        {
            throw new InvalidDataException();
        }
        var result = new OpenIddictApplicationDescriptor
        {
            ClientId = row.ClientId,
            DisplayName = row.DisplayName,
            ApplicationType = row.ApplicationType,
            ClientType = row.ClientType,
            ConsentType = row.ConsentType
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
    private sealed record Row(string Id, string ClientId, string? DisplayName, string? ClientType, string? Redirects, string? Permissions, string? Requirements, string? ApplicationType, string? ConsentType, bool NoSecret, bool HasSecret, bool HasKey, bool NativeProfile);
}

/// <summary>Retains one bounded already validated native observation without exposing additional fields through existing editor contracts.</summary>
internal sealed record ApplicationDetailsSnapshot(ApplicationDetails? Details, string? Version, IReadOnlyList<string>? Scopes);
