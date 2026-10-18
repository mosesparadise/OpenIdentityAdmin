using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Reconciles the native validated reference with one final untracked persisted scalar snapshot after all quota queries.</summary>
internal static class NativeStatePersistence
{
    /// <summary>Retains native reference hashing/filtering, then verifies row data and relationships without a later manager query.</summary>
    internal static async ValueTask<bool> MatchesAsync(ProtocolStorageScope scope, OpenIddictClientEvents.ProcessChallengeContext context,
        NativeStateGeneration proof, CancellationToken token)
    {
        var principal = context.StateTokenPrincipal ?? throw ProtocolStorageOwner.Unavailable();
        var options = scope.Services.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;
        if (!options.DisableEntityCaching || options.DisableAdditionalFiltering)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var resolved = await manager.FindByReferenceIdAsync(context.StateToken!, token);
        if (resolved is null || await manager.GetIdAsync(resolved, token) != principal.GetTokenId())
        {
            return false;
        }
        var reference = await manager.GetReferenceIdAsync(resolved, token);
        return await scope.Owner.RunAsync(async cancellation =>
        {
            var identifier = principal.GetTokenId();
            var row = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
                .Where(value => value.Id == identifier).Select(value => new
                {
                    value.Type,
                    value.Status,
                    value.Subject,
                    value.CreationDate,
                    value.ExpirationDate,
                    value.ReferenceId,
                    value.Payload,
                    Application = value.Application != null,
                    Authorization = value.Authorization != null
                }).SingleOrDefaultAsync(cancellation);
            return row is not null && !row.Application && !row.Authorization && row.Subject is null &&
                row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken && row.Status == OpenIddictConstants.Statuses.Valid &&
                row.CreationDate == principal.GetCreationDate()?.UtcDateTime &&
                row.ExpirationDate == principal.GetExpirationDate()?.UtcDateTime &&
                !string.IsNullOrEmpty(reference) && row.ReferenceId == reference && proof.MatchesPayload(row.Payload);
        }, token);
    }
}
