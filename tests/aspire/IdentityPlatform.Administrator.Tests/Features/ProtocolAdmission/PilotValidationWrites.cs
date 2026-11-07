using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Correlates successful native consumption and generation saves with the independently observed gate contexts.</summary>
internal sealed class PilotValidationWrites(PilotValidationProbe probe) : SaveChangesInterceptor
{
    private bool consumption;
    private bool generation;

    /// <summary>Recognizes actual pending native mutations without issuing a replacement write.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context!.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().ToArray();
        consumption = entries.Any(entry => entry.State == EntityState.Modified &&
            entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode && entry.Entity.Status == OpenIddictConstants.Statuses.Redeemed);
        generation = entries.Any(entry => entry.State == EntityState.Added && entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        return ValueTask.FromResult(result);
    }

    /// <summary>Records identities only after native SaveChanges actually returns; the HTTP test independently verifies persisted lifecycle and cryptography.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (consumption)
        {
            Assert.Equal(Guid.Empty, probe.Consumption);
            probe.Consumption = eventData.Context!.ContextId.InstanceId;
        }
        if (generation)
        {
            Assert.Equal(Guid.Empty, probe.Generation);
            probe.Generation = eventData.Context!.ContextId.InstanceId;
        }
        consumption = generation = false;
        return ValueTask.FromResult(result);
    }
}
