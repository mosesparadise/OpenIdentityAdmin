using System.Collections.Immutable;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stores the original catalogue identifiers with the native machine token so later reassignments cannot free those names for reuse.</summary>
internal sealed class MachineTokenReferences(ImmutableArray<ApplicationScopeSelection.Entry> entries)
{
    private const string ScopePrefix = "managed_scope_";
    private const string AudiencePrefix = "managed_audience_";

    /// <summary>Persists bounded nonsecret scope and audience digests on the original native token in its issuance transaction.</summary>
    internal async ValueTask SaveAsync(ProtocolStorageScope scope, string tokenId, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(tokenId, token) ?? throw ProtocolStorageOwner.Unavailable();
        var descriptor = new OpenIddictTokenDescriptor();
        await manager.PopulateAsync(descriptor, row, token);
        if (descriptor.Properties.Count != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        foreach (var marker in Markers())
        {
            descriptor.Properties.Add(marker, JsonSerializer.SerializeToElement(true));
        }
        await manager.UpdateAsync(row, descriptor, token);
    }

    /// <summary>Rechecks the exact native token associations after the last issuer save.</summary>
    internal async ValueTask<bool> MatchesAsync(ProtocolStorageScope scope, string tokenId, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(tokenId, token);
        if (row is null || !await ProtocolStorageRows.ReloadAsync(scope, row, token))
        {
            return false;
        }
        var properties = await manager.GetPropertiesAsync(row, token);
        return Markers().SetEquals(properties.Keys) && properties.Values.All(value => value.ValueKind == JsonValueKind.True);
    }

    /// <summary>Checks for a retained token marker using the native store before the selected scope or audience may be deleted.</summary>
    internal static async Task<bool> RetainedAsync(IServiceProvider services, string id, bool audience, CancellationToken token)
    {
        var marker = JsonSerializer.Serialize((audience ? AudiencePrefix : ScopePrefix) + ManagedLoginRules.Digest(id));
        var store = services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        var ids = store.ListAsync((rows, value) => rows.Where(row => row.Properties != null && row.Properties.Contains(value))
            .Select(row => row.Id).Take(1), marker, token);
        await foreach (var unused in ids.WithCancellation(token))
        {
            return true;
        }
        token.ThrowIfCancellationRequested();
        return false;
    }

    /// <summary>Separates the scope and audience namespaces, using only immutable original database identities.</summary>
    private HashSet<string> Markers()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            keys.Add(ScopePrefix + ManagedLoginRules.Digest(entry.Scope.Id));
            keys.Add(AudiencePrefix + ManagedLoginRules.Digest(entry.Audience!.Id));
        }
        return keys;
    }
}
