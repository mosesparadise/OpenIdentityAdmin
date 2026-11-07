using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Finds exact retained native dependencies through bounded read-only store projections without creating a second assignment catalogue.</summary>
internal static class CatalogueNativeReferences
{
    private const int MaximumCandidates = 128;
    private const int MaximumJsonCharacters = 32_768;
    private const int Overflow = 1;
    private const int JsonDepth = 2;
    private const string Escape = "\\";

    /// <summary>Includes unmanaged native scopes and escaped JSON encodings; only decoded ordinal resources count as a reference.</summary>
    internal static Task<bool> AudienceAsync(ProtocolStorageScope scope, string resource, CancellationToken token)
    {
        var store = scope.Services.GetRequiredService<IOpenIddictScopeStore<OpenIddictEntityFrameworkCoreScope>>();
        var quoted = JsonSerializer.Serialize(resource);
        var candidates = store.ListAsync((rows, value) => rows.Where(row => row.Resources != null &&
                (row.Resources.Contains(value) || row.Resources.Contains(Escape))).OrderBy(row => row.Id)
            .Take(MaximumCandidates + Overflow).Select(row => row.Resources!.Substring(0, MaximumJsonCharacters + Overflow)), quoted, token);
        return ContainsAsync(candidates, resource, token);
    }

    /// <summary>Includes every retained native authorization status and exact application scope permission, not just currently valid grants.</summary>
    internal static async Task<bool> ScopeAsync(ProtocolStorageScope scope, string name, CancellationToken token)
    {
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication>>();
        var permission = OpenIddictConstants.Permissions.Prefixes.Scope + name;
        var quotedPermission = JsonSerializer.Serialize(permission);
        var assigned = applications.ListAsync((rows, value) => rows.Where(row => row.Permissions != null &&
                (row.Permissions.Contains(value) || row.Permissions.Contains(Escape))).OrderBy(row => row.Id)
            .Take(MaximumCandidates + Overflow).Select(row => row.Permissions!.Substring(0, MaximumJsonCharacters + Overflow)), quotedPermission, token);
        if (await ContainsAsync(assigned, permission, token))
        {
            return true;
        }
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization>>();
        var quotedName = JsonSerializer.Serialize(name);
        var retained = authorizations.ListAsync((rows, value) => rows.Where(row => row.Scopes != null &&
                (row.Scopes.Contains(value) || row.Scopes.Contains(Escape))).OrderBy(row => row.Id)
            .Take(MaximumCandidates + Overflow).Select(row => row.Scopes!.Substring(0, MaximumJsonCharacters + Overflow)), quotedName, token);
        return await ContainsAsync(retained, name, token);
    }

    /// <summary>Never converts malformed, oversized, truncated, overflowing or cancelled observations into an unreferenced result.</summary>
    private static async Task<bool> ContainsAsync(IAsyncEnumerable<string> candidates, string expected, CancellationToken token)
    {
        var count = 0;
        await foreach (var json in candidates.WithCancellation(token))
        {
            if (++count > MaximumCandidates || json.Length > MaximumJsonCharacters)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = JsonDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            var matched = false;
            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw ProtocolStorageOwner.Unavailable();
                }
                matched |= string.Equals(value.GetString(), expected, StringComparison.Ordinal);
            }
            if (matched)
            {
                return true;
            }
        }
        token.ThrowIfCancellationRequested();
        return false;
    }
}
